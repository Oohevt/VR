# 星空深睡：睡眠疗愈仓 Unity MVP

这个版本用于在没有 PICO 真机和真实脑电 SDK 的情况下验证体验流程。它包含 90 秒演示与 8 分钟完整流程，用模拟脑电控制星尘密度、呼吸光强、环境运动速度和程序化低频声音。

## 准备 PICO SDK

仓库不包含 PICO 官方 SDK。请从 PICO 开发者平台下载 `PICO Unity Integration SDK 3.4.0`，解压到仓库同级的 `SDKs/PICO-Unity-Integration-SDK-3.4.0`。项目通过 `Packages/manifest.json` 中的相对路径引用该目录；若放在其他位置，需要相应修改 `com.unity.xr.picoxr` 的本地路径。

## 在 Mac 上运行

直接打开 `Builds/macOS/SleepHealingPreview.app`。应用启动后会自动进入 90 秒演示模式，也可以在底部切换为 8 分钟完整模式。

底部按钮用于重新开始、暂停和安全退出。按空格可暂停或继续，按 `Esc` 可回到静止安全画面。右侧脑电模拟器支持自动曲线、手动调节、模拟断联和注入瞬时尖峰。

## 五阶段流程

流程依次经过静心准备、基线采集、呼吸放松、深度疗愈和自然唤醒。计时使用不受帧率影响的时间增量，暂停期间不推进阶段。信号质量不足时，场景保留最后一个稳定状态并冻结自适应。

## 渲染与视觉

项目使用 URP 17.5（Unity 6.5 内置）。管线资产在 `Assets/Rendering/`，由 `Sleep Healing/Setup Render Pipeline` 生成；构建入口会自动检查并补齐，不需要手动执行。

视觉全部在运行时生成：`StarSky.cs` 在启动时并行计算一张 HDR 星空立方体贴图（渐变、银河带、幂律亮度恒星），`HealingEnvironment.cs` 生成镜面舱台、十二条穹顶光弧、呼吸光核与近景星尘，并挂一个全局 Volume（Bloom、ACES 色调映射、Vignette、色彩微调）。亮度超过 1.0 的发光体由 Bloom 产生辉光。

`Assets/Resources/SleepHealing/` 里的几个材质只是模板，作用是把 URP 着色器和所需关键字变体带进打包；运行时代码复制模板再改颜色。

## 真实脑电接入点

`IEEGStateProvider` 是统一数据入口，`SimulatedEEGAdapter` 提供当前模拟数据。拿到改装 PICO 的上位机源码或 SDK 后，在 `PicoEEGAdapter` 内完成协议转换并发布 `EEGState`，场景、状态机和安全逻辑不需要读取厂商原始字段。

真实字段、通信协议、采样率和算法含义均为“待 SDK 确认”。当前状态值只用于交互演示，不构成医疗诊断或疗效结论。

## 构建与验证

Unity 菜单 `Sleep Healing` 下提供场景准备、渲染管线装配、macOS 构建和 Android 构建入口。自动化入口为 `SleepHealing.Editor.SleepHealingBuild`。

`Tools/capture-preview.sh` 一条命令完成 macOS 构建、启动预览版自动截取五阶段画面到 `Artifacts/`、拼成 `Artifacts/五阶段预览.png`，并复制一份带时间戳的副本到桌面（桌面只保留最新一份）。加 `--no-build` 可跳过构建只截图。

Android 构建目前受项目路径限制：Unity 6.5 的 Android 工具拒绝包含非 ASCII 字符的项目绝对路径。把项目放在纯英文路径后即可继续生成 APK；处理方式记录在 `BLOCKED.md`。

## 素材与许可

空间、星尘、舱体、呼吸光和声音全部由运行时程序生成。中文界面使用 `Assets/Fonts/NotoSansCJKsc-Regular.otf`，来源为 notofonts/noto-cjk，许可为 SIL Open Font License 1.1，完整条款见同目录 `OFL-LICENSE.txt`。

## 未完成的真机验证

PICO 头部追踪、双眼尺度、真机帧率、USB 调试、改装电极佩戴稳定性和真实脑电数据均未验证。URP 后处理（Bloom + HDR 中间缓冲 + 4x MSAA）在 XR2 Gen 2 上的帧率也未验证，真机掉帧时优先关 Bloom 或把渲染管线资产的 Render Scale 降到 0.85。真机回来后需要重新检查这些项目，桌面预览不能替代头显验收。
