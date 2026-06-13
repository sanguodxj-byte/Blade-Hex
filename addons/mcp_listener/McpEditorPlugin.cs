using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BladeHex.Addons.McpListener
{
    [Tool]
    public partial class McpEditorPlugin : EditorPlugin
    {
        private const int Port = 5005;
        private TcpListener? _listener;
        private Thread? _listenerThread;
        private bool _running;

        // 线程安全的 Action 队列，用于将后台 TCP 线程接收到的反射指令调度到 Godot 主线程执行
        private readonly ConcurrentQueue<Action> _actionQueue = new();

        public override void _EnterTree()
        {
            GD.Print("[McpEditorPlugin] Activating MCP Editor Plugin Listener...");
            SetProcess(true);
            StartServer();
        }

        public override void _ExitTree()
        {
            GD.Print("[McpEditorPlugin] Deactivating MCP Editor Plugin Listener...");
            StopServer();
        }

        public override void _Process(double delta)
        {
            // 每帧轮询并消耗主线程 Action
            while (_actionQueue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[McpEditorPlugin] Error running main thread action: {ex.Message}");
                }
            }
        }

        private void StartServer()
        {
            try
            {
                _running = true;
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();

                _listenerThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "McpListenerThread"
                };
                _listenerThread.Start();
                GD.Print($"[McpEditorPlugin] Background TcpListener started on 127.0.0.1:{Port}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[McpEditorPlugin] Failed to start TcpListener: {ex.Message}");
            }
        }

        private void StopServer()
        {
            _running = false;
            try
            {
                _listener?.Stop();
                _listener = null;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[McpEditorPlugin] Error during listener stop: {ex.Message}");
            }

            if (_listenerThread != null && _listenerThread.IsAlive)
            {
                _listenerThread.Join(500);
            }
            _listenerThread = null;
            GD.Print("[McpEditorPlugin] TcpListener stopped.");
        }

        private void ListenLoop()
        {
            while (_running && _listener != null)
            {
                try
                {
                    if (!_listener.Pending())
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    var client = _listener.AcceptTcpClient();
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (SocketException)
                {
                    // 正常退出（Stop 调用时 Accept 会抛 SocketException）
                    break;
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[McpEditorPlugin] Exception in listen loop: {ex.Message}");
                    Thread.Sleep(1000);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            GD.Print($"[McpEditorPlugin] Remote MCP client connected: {client.Client.RemoteEndPoint}");
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream) { AutoFlush = true })
            {
                try
                {
                    while (_running && client.Connected)
                    {
                        var line = await reader.ReadLineAsync();
                        if (line == null) break;

                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // 使用 TaskCompletionSource 同步等待主线程执行结果并取回 JSON 字符串
                        var tcs = new TaskCompletionSource<string>();
                        _actionQueue.Enqueue(() =>
                        {
                            try
                            {
                                var resultObj = ExecuteCommandOnMainThread(line);
                                tcs.SetResult(JsonSerializer.Serialize(resultObj));
                            }
                            catch (Exception ex)
                            {
                                tcs.SetResult(JsonSerializer.Serialize(new
                                {
                                    status = "error",
                                    message = ex.Message
                                }));
                            }
                        });

                        string responseJson = await tcs.Task;
                        await writer.WriteLineAsync(responseJson);
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[McpEditorPlugin] Exception in client handler: {ex.Message}");
                }
            }
            GD.Print("[McpEditorPlugin] MCP client disconnected.");
        }

        private object ExecuteCommandOnMainThread(string requestJson)
        {
            using var doc = JsonDocument.Parse(requestJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("command", out var cmdProp))
            {
                return new { status = "error", message = "Missing 'command' field in JSON request." };
            }

            string command = cmdProp.GetString() ?? "";

            switch (command)
            {
                case "ping":
                    return new { status = "success", message = "pong", version = "1.0.0" };

                case "get_selection":
                    {
                        var selection = EditorInterface.Singleton.GetSelection().GetSelectedNodes();
                        var paths = new List<string>();
                        foreach (var node in selection)
                        {
                            paths.Add(node.GetPath().ToString());
                        }
                        return new { status = "success", selection = paths };
                    }

                case "select_node":
                    {
                        if (!root.TryGetProperty("path", out var pathProp))
                        {
                            return new { status = "error", message = "Missing 'path' parameter for 'select_node'." };
                        }

                        string path = pathProp.GetString() ?? "";
                        var sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
                        if (sceneRoot == null)
                        {
                            return new { status = "error", message = "No active edited scene root found." };
                        }

                        Node? target = null;
                        if (path == sceneRoot.Name || path == "." || string.IsNullOrEmpty(path))
                        {
                            target = sceneRoot;
                        }
                        else
                        {
                            target = sceneRoot.GetNodeOrNull(path) ?? FindNodeRecursive(sceneRoot, path);
                        }

                        if (target == null)
                        {
                            return new { status = "error", message = $"Node not found for path or pattern: '{path}'" };
                        }

                        var sel = EditorInterface.Singleton.GetSelection();
                        sel.Clear();
                        sel.AddNode(target);
                        EditorInterface.Singleton.InspectObject(target);

                        return new { status = "success", selected_path = target.GetPath().ToString() };
                    }

                case "teleport_camera":
                    {
                        if (!root.TryGetProperty("path", out var pathProp))
                        {
                            return new { status = "error", message = "Missing 'path' parameter for 'teleport_camera'." };
                        }

                        string path = pathProp.GetString() ?? "";
                        var sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
                        if (sceneRoot == null)
                        {
                            return new { status = "error", message = "No active edited scene root found." };
                        }

                        Node? target = null;
                        if (path == sceneRoot.Name || path == "." || string.IsNullOrEmpty(path))
                        {
                            target = sceneRoot;
                        }
                        else
                        {
                            target = sceneRoot.GetNodeOrNull(path) ?? FindNodeRecursive(sceneRoot, path);
                        }

                        if (target == null)
                        {
                            return new { status = "error", message = $"Node not found for camera teleport: '{path}'" };
                        }

                        // 默认先选中并 Inspect
                        var sel = EditorInterface.Singleton.GetSelection();
                        sel.Clear();
                        sel.AddNode(target);
                        EditorInterface.Singleton.InspectObject(target);

                        if (target is Node3D node3D)
                        {
                            // 从 3D 编辑器视口获取主相机并移动
                            var viewport3D = EditorInterface.Singleton.GetEditorViewport3D(0);
                            if (viewport3D != null)
                            {
                                var camera = viewport3D.GetCamera3D();
                                if (camera != null)
                                {
                                    var targetPos = node3D.GlobalPosition;
                                    // 倾斜偏移聚焦
                                    camera.GlobalPosition = targetPos + new Vector3(0, 1.5f, 3.0f);
                                    camera.LookAt(targetPos);
                                    return new
                                    {
                                        status = "success",
                                        message = $"Focused 3D camera to node: {node3D.Name} ({target.GetPath()})"
                                    };
                                }
                                return new { status = "error", message = "Editor 3D Camera is null." };
                            }
                            return new { status = "error", message = "Editor 3D Viewport is null." };
                        }
                        else if (target is Node2D node2D)
                        {
                            return new
                            {
                                status = "success",
                                message = $"Selected 2D Node: {node2D.Name}. Teleport camera skipped (Node is 2D)."
                            };
                        }

                        return new
                        {
                            status = "success",
                            message = $"Selected Node: {target.Name}. Node is not Node3D or Node2D, camera teleport skipped."
                        };
                    }

                case "hot_reload_assets":
                    {
                        var editorFs = EditorInterface.Singleton.GetResourceFilesystem();
                        if (root.TryGetProperty("files", out var filesProp) && filesProp.ValueKind == JsonValueKind.Array)
                        {
                            var packed = new List<string>();
                            foreach (var f in filesProp.EnumerateArray())
                            {
                                var pathStr = f.GetString();
                                if (!string.IsNullOrEmpty(pathStr)) packed.Add(pathStr);
                            }
                            editorFs.ReimportFiles(packed.ToArray());
                            return new { status = "success", message = $"Forced reimport of {packed.Count} files." };
                        }
                        else
                        {
                            editorFs.Scan();
                            return new { status = "success", message = "Triggered incremental project file scan." };
                        }
                    }

                case "force_cold_reimport":
                    {
                        if (!root.TryGetProperty("file_path", out var fileProp))
                        {
                            return new { status = "error", message = "Missing 'file_path' parameter." };
                        }
                        string filePath = fileProp.GetString() ?? "";
                        var fs2 = EditorInterface.Singleton.GetResourceFilesystem();
                        // Godot 4.x EditorFileSystem 没有 ScanFiles，使用 Scan() 触发增量扫描
                        // 或者用 ReimportFiles 对单个文件强制重导入
                        var reimportList = new string[] { filePath };
                        fs2.ReimportFiles(reimportList);
                        return new { status = "success", message = $"Triggered reimport for: {filePath}" };
                    }

                case "get_active_scene_context":
                    {
                        var sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
                        if (sceneRoot == null)
                        {
                            return new { status = "success", message = "No active scene opened in editor." };
                        }
                        var selection = EditorInterface.Singleton.GetSelection().GetSelectedNodes();
                        var selectedPaths = new List<string>();
                        foreach (var node in selection)
                        {
                            selectedPaths.Add(node.GetPath().ToString());
                        }
                        return new
                        {
                            status = "success",
                            scene_path = sceneRoot.SceneFilePath,
                            scene_root_name = sceneRoot.Name.ToString(),
                            scene_root_type = sceneRoot.GetType().Name,
                            selected_nodes = selectedPaths
                        };
                    }

                case "capture_viewport_render":
                    {
                        var viewport3D = EditorInterface.Singleton.GetEditorViewport3D(0);
                        if (viewport3D == null)
                        {
                            return new { status = "error", message = "3D Editor Viewport is null." };
                        }

                        var texture = viewport3D.GetTexture();
                        if (texture == null)
                        {
                            return new { status = "error", message = "Failed to get viewport texture." };
                        }

                        var image = texture.GetImage();
                        if (image == null)
                        {
                            return new { status = "error", message = "Failed to get image from viewport texture." };
                        }

                        string scratchDir = System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "scratch");
                        if (!System.IO.Directory.Exists(scratchDir))
                        {
                            System.IO.Directory.CreateDirectory(scratchDir);
                        }

                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string fileName = $"viewport_capture_{timestamp}.png";
                        string physicalPath = System.IO.Path.Combine(scratchDir, fileName);

                        var err = image.SavePng(physicalPath);
                        if (err != Error.Ok)
                        {
                            return new { status = "error", message = $"Failed to save viewport capture. Error: {err}" };
                        }

                        return new { status = "success", image_path = $"scratch/{fileName}", physical_path = physicalPath };
                    }

                default:
                    return new { status = "error", message = $"Unknown TCP command: '{command}'" };
            }
        }

        private Node? FindNodeRecursive(Node current, string pattern)
        {
            // 1. 节点名全词精准一致校验
            if (current.Name.ToString().Equals(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            // 2. 节点完整路径片段完全后缀一致校验，彻底杜绝由于 EndsWith 部分重叠（如 ArrowHead 错判为 Head）引起的误报
            string fullPath = current.GetPath().ToString();
            string cleanPattern = pattern.Replace('\\', '/').TrimStart('/');

            if (fullPath.EndsWith("/" + cleanPattern, StringComparison.OrdinalIgnoreCase) ||
                fullPath.Equals(cleanPattern, StringComparison.OrdinalIgnoreCase) ||
                fullPath.Equals("/root/" + cleanPattern, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            foreach (var child in current.GetChildren())
            {
                var found = FindNodeRecursive(child, pattern);
                if (found != null) return found;
            }

            return null;
        }
    }
}
