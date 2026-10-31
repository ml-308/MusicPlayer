$ErrorActionPreference = 'SilentlyContinue'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host ''
Write-Host '  轻音播放器 - 启动弹窗(Windows 无法识别的应用)处理工具'
Write-Host '  ------------------------------------------------------'
Write-Host ''
Write-Host '  [当前状态]'
$targets = @()
$targets += (Join-Path $dir 'MusicPlayer.exe')
foreach ($p in @("$env:USERPROFILE\Downloads\MusicPlayer.exe", "$env:USERPROFILE\Desktop\MusicPlayer.exe")) {
    if (Test-Path -LiteralPath $p) { $targets += $p }
}
foreach ($t in ($targets | Select-Object -Unique)) {
    if (-not (Test-Path -LiteralPath $t)) { continue }
    $z = Get-Item -LiteralPath $t -Stream Zone.Identifier
    if ($z) { Write-Host ('   ' + $t + '   [有下载标记]') -ForegroundColor Yellow }
    else { Write-Host ('   ' + $t + '   [正常]') -ForegroundColor Green }
}
$appHost = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost'
$explorer = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer'
$eval = (Get-ItemProperty -Path $appHost -Name EnableWebContentEvaluation).EnableWebContentEvaluation
$ss = (Get-ItemProperty -Path $explorer -Name SmartScreenEnabled).SmartScreenEnabled
if ($eval -eq 0) { Write-Host '   检查应用和文件：已关闭' -ForegroundColor Green }
else { Write-Host '   检查应用和文件：开启（这就是弹窗的来源）' -ForegroundColor Yellow }
if ($ss -eq 'Off') { Write-Host '   SmartScreen：已关闭' -ForegroundColor Green }
else { Write-Host '   SmartScreen：默认(开启)' }
Write-Host ''
Write-Host '  请选择：'
Write-Host '    1  只清除下载标记（不改任何系统安全设置）'
Write-Host '    2  清除下载标记 + 关闭「检查应用和文件」（彻底不再弹窗）'
Write-Host '    3  恢复 Windows 默认安全设置（撤销选项 2）'
Write-Host '    4  打开「应用和浏览器控制」设置页（手动关闭那个开关）'
Write-Host '    5  以后用 启动播放器.bat 启动（不经过外壳检查，不会弹窗）'
Write-Host '    0  退出'
Write-Host ''
$choice = Read-Host '  输入 1 / 2 / 3 / 4 / 5 / 0 后回车'
Write-Host ''
if ($choice -eq '0') { return }
if ($choice -eq '1' -or $choice -eq '2') {
    $n = 0
    foreach ($t in ($targets | Select-Object -Unique)) {
        $folder = Split-Path -Parent $t
        Get-ChildItem -LiteralPath $folder -Filter '*.exe' -File | ForEach-Object {
            if (Get-Item -LiteralPath $_.FullName -Stream Zone.Identifier) {
                Unblock-File -LiteralPath $_.FullName
                $n++
                Write-Host ('   已解除：' + $_.FullName) -ForegroundColor Green
            }
        }
    }
    Write-Host ('  [完成] 清除下载标记：' + $n + ' 个文件') -ForegroundColor Green
    if ($choice -eq '2') {
        $ok = $false
        try {
            New-Item -Path $appHost -Force -ErrorAction Stop | Out-Null
            New-Item -Path $explorer -Force -ErrorAction Stop | Out-Null
            Set-ItemProperty -Path $appHost -Name EnableWebContentEvaluation -Value 0 -Type DWord -ErrorAction Stop
            Set-ItemProperty -Path $explorer -Name SmartScreenEnabled -Value 'Off' -Type String -ErrorAction Stop
            $v = (Get-ItemProperty -Path $appHost -Name EnableWebContentEvaluation).EnableWebContentEvaluation
            if ($v -eq 0) { $ok = $true }
        } catch { $ok = $false }
        if ($ok) {
            Write-Host '  [完成] 已关闭「检查应用和文件」，立即生效，以后双击 exe 也不会再弹窗' -ForegroundColor Green
        } else {
            Write-Host '  [提示] 系统拒绝自动修改（篡改防护），请用选项 4 手动关闭' -ForegroundColor Yellow
        }
    }
}
elseif ($choice -eq '3') {
    if (Get-Item -Path $appHost) { Set-ItemProperty -Path $appHost -Name EnableWebContentEvaluation -Value 1 -Type DWord }
    if (Get-Item -Path $explorer) { Set-ItemProperty -Path $explorer -Name SmartScreenEnabled -Value 'Warn' -Type String }
    Write-Host '  [完成] 已恢复 Windows 默认设置' -ForegroundColor Green
}
elseif ($choice -eq '4') {
    Write-Host '  正在打开 Windows 安全中心...' 
    Start-Process 'windowsdefender://appbrowser'
    Write-Host ''
    Write-Host '  在打开的窗口里按这个路径点：' -ForegroundColor Cyan
    Write-Host '    「应用和浏览器控制」 -> 「基于声誉的保护」'
    Write-Host '    -> 把「检查应用和文件」关掉'
    Write-Host '  （关掉立即生效，不用重启；Defender 的病毒防护仍然开启，随时可以再打开）'
}
elseif ($choice -eq '5') {
    $bat = Join-Path $dir '启动播放器.bat'
    if (Test-Path -LiteralPath $bat) {
        Write-Host '  说明：通过 启动播放器.bat 启动，不经过资源管理器的外壳检查，不会弹窗。' -ForegroundColor Cyan
        Write-Host '        可以直接给它建一个桌面快捷方式，以后双击快捷方式即可。'
        Start-Process -FilePath $bat -WorkingDirectory $dir
    } else { Write-Host '  找不到 启动播放器.bat' -ForegroundColor Yellow }
}
else { Write-Host '  没有选择任何操作。' }
Write-Host ''
Write-Host '  说明：这个弹窗是 Windows 对「未签名程序」的信誉拦截，不是病毒；'
Write-Host '        本程序不联网、不写注册表、对音乐文件只读。'
Write-Host ''
