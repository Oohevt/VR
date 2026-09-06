# 待裁决与已知限制

## 已解释的任务 0 差异

- 任务书记录 `Assets` 3 个文件、缓存外项目文件 28 个；2026-09-04 复跑结果为 11 和 36。
- 原因：任务书生成后，领导已批准并完成 4 个 C# 预热文件及其 4 个 `.meta` 文件。
- 冻结的 Unity 版本、PICO 包路径和两个 SHA-256 均一致，因此该差异不影响后续任务。

## 当前无法验证

- PICO 真机不在现场，无法验证头部追踪、双眼视觉、真机帧率、USB 调试和实际佩戴舒适度。
- 改装 PICO 上位机源码与脑电 SDK 尚未提供，真实字段、协议、采样率和算法含义保持“待 SDK 确认”。
- Android 构建被 Unity 拒绝：当前项目上级目录含非 ASCII 字符。解决需要把项目放到纯英文路径，或从纯英文路径建立项目链接。
- 截图改为 `ScreenCapture` 抓最终画面（含后处理与界面），之前 Camera.Render 到 RenderTexture 的 Metal 分块缺失问题不再出现。
- PICO SDK 3.4.0 的调试器在开发构建启动时读取 `Resources/PXR_PicoDebuggerSO`，缺失会抛 NullReference 并弹出开发控制台；构建脚本现在自动生成该资产（isOpen=false）。
- URP 后处理在 PICO 4 Ultra 真机上的帧率未验证。
