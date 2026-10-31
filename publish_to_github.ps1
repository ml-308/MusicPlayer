param([switch]$DryRun)
$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $dir
$Version = '1.0.0'
$ZipPath = Join-Path $dir ('dist\MusicPlayer-' + $Version + '-portable.zip')
$NL = [Environment]::NewLine

function Step($t) { Write-Host ''; Write-Host ('== ' + $t + ' ==') -ForegroundColor Cyan }
function Ok($t)   { Write-Host ('   [OK] ' + $t) -ForegroundColor Green }
function Warn($t) { Write-Host ('   [!]  ' + $t) -ForegroundColor Yellow }
function Die($t)  { Write-Host ('   [X]  ' + $t) -ForegroundColor Red; Write-Host ''; Read-Host '按回车退出'; exit 1 }

Write-Host ''
Write-Host '  轻音播放器 - 发布到 GitHub（新建仓库 / 推送 / 发布 Release）'
Write-Host '  ----------------------------------------------------------'
if ($DryRun) { Warn '演练模式：只做本地准备，不联网、不推送' }

Step '检查环境'
$git = Get-Command git -ErrorAction SilentlyContinue
if (-not $git) { Die '未找到 git，请先安装 Git for Windows' }
Ok 'git 已安装'
if (-not (Test-Path -LiteralPath $ZipPath)) {
    Die ('没有找到发布包：' + $ZipPath + '（请先在 src 目录运行 build.bat 生成 exe，再用 dist 打包）')
}
Ok ('发布包：' + $ZipPath)

Step 'GitHub 账号信息'
Write-Host '  提示：浏览器里保存的密码无法（也不应该）被脚本读取。'
Write-Host '        请打开 https://github.com/settings/tokens 生成 Personal Access Token，'
Write-Host '        勾选 repo 权限即可，下面粘贴（输入时不显示）。'
Write-Host ''
$user = Read-Host '  GitHub 用户名'
if (-not $user) { Die '用户名不能为空' }
$repoIn = Read-Host '  仓库名（直接回车使用 light-music-player）'
$repo = if ($repoIn) { $repoIn } else { 'light-music-player' }
$private = (Read-Host '  仓库是否私有？(y/N)') -match '^[Yy]'

if ($DryRun) {
    $tok = ''
} else {
    $sec = Read-Host '  粘贴 Personal Access Token' -AsSecureString
    $tok = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
    if (-not $tok) { Die 'Token 不能为空' }
}

Step '本地仓库提交'
if (-not (Test-Path -LiteralPath (Join-Path $dir '.git'))) {
    & git init -b main -q
    Ok '已初始化仓库'
}
$name = $user
$email = $user + '@users.noreply.github.com'
& git -c user.name="$name" -c user.email="$email" add -A
& git -c user.name="$name" -c user.email="$email" commit -q -m ('release: v' + $Version) 2>$null
if ($LASTEXITCODE -eq 0) { Ok '已提交改动' } else { Ok '没有新的改动' }
& git -c user.name="$name" -c user.email="$email" tag -f ('v' + $Version) 2>$null
if ($LASTEXITCODE -eq 0) { Ok ('已标记 v' + $Version) } else { Warn ('标签 v' + $Version + ' 创建失败（可能是与已有标签冲突）') }

if ($DryRun) {
    Write-Host ''
    Ok '演练结束（未联网）。真实执行时会：'
    Write-Host ('     1) 创建/复用仓库 https://github.com/' + $user + '/' + $repo)
    Write-Host ('     2) git push 到 main，并推送标签 v' + $Version)
    Write-Host ('     3) 创建 Release v' + $Version + ' 并上传 portable.zip')
    Write-Host ''
    Read-Host '按回车退出'
    exit 0
}

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$hdr = @{ Authorization = 'token ' + $tok; 'User-Agent' = 'light-music-player-publisher'; 'Accept' = 'application/vnd.github+json' }

Step '校验 Token'
try {
    $me = Invoke-RestMethod -Uri 'https://api.github.com/user' -Headers $hdr -Method Get -TimeoutSec 30
    Ok ('已登录：' + $me.login)
} catch {
    Die ('Token 无效或网络不通：' + $_.Exception.Message)
}

Step '创建仓库'
$repoBody = @{ name = $repo; description = '轻量的 Windows 本地音乐播放器：文件夹扫描、四种播放模式、桌面歌词，免安装单文件 exe'; private = $private; has_issues = $true; has_wiki = $false } | ConvertTo-Json
try {
    $r = Invoke-RestMethod -Uri 'https://api.github.com/user/repos' -Headers $hdr -Method Post -Body $repoBody -ContentType 'application/json' -TimeoutSec 30
    Ok ('已创建：' + $r.html_url)
} catch {
    $msg = $_.Exception.Message
    if ($msg -match '422') { Warn '仓库已存在，继续使用现有仓库' }
    else { Die ('创建仓库失败：' + $msg) }
}
$repoUrl = 'https://github.com/' + $user + '/' + $repo + '.git'

Step '推送代码'
& git remote remove origin 2>$null
& git remote add origin $repoUrl
$b64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($user + ':' + $tok))
$authHeader = 'AUTHORIZATION: basic ' + $b64
& git -c http.extraHeader="$authHeader" -c credential.helper= push -u origin main 2>&1 | ForEach-Object { Write-Host ('   ' + $_) }
if ($LASTEXITCODE -ne 0) { Die '推送失败（检查 Token 是否具备 repo 权限）' }
Ok 'main 已推送'

& git -c http.extraHeader="$authHeader" -c credential.helper= push origin ('v' + $Version) 2>&1 | ForEach-Object { Write-Host ('   ' + $_) }
Ok '标签已推送'

Step ('创建 Release v' + $Version)
$bodyLines = @(
    '轻量、免安装的 Windows 本地音乐播放器。',
    '',
    '## 功能',
    '- 读取整个文件夹的音乐信息（只读标签，不加载整首音频）',
    '- 顺序 / 列表循环 / 单曲循环 / 随机 四种播放模式',
    '- 桌面歌词：逐字卡拉 OK 高亮、双语对照、下一句预告',
    '- 支持 MP3 / WAV / WMA / FLAC / M4A / AAC / OGG / Opus / AIFF / APE',
    '- 托盘控制、定时停止、m3u 导入导出、封面显示',
    '',
    '## 使用',
    '1. 下载 MusicPlayer-' + $Version + '-portable.zip 并解压',
    '2. 双击 启动播放器.bat 运行（可绕过 SmartScreen 对未签名程序的拦截提示）',
    '3. 也可以直接双击 MusicPlayer.exe',
    '',
    '## 说明',
    '- 首次运行生成的 config/cache 只保存在程序目录，不联网、不写注册表',
    '- 源码在 src/，双击 src\build.bat 可自行重新编译'
)
$relBody = @{
    tag_name = 'v' + $Version
    target_commitish = 'main'
    name = '轻音播放器 v' + $Version
    body = [string]::Join($NL, $bodyLines)
    draft = $false
    prerelease = $false
} | ConvertTo-Json
try {
    $rel = Invoke-RestMethod -Uri ('https://api.github.com/repos/' + $user + '/' + $repo + '/releases') -Headers $hdr -Method Post -Body $relBody -ContentType 'application/json' -TimeoutSec 30
    Ok ('Release 已创建：' + $rel.html_url)
} catch {
    if ($_.Exception.Message -match '422') {
        Warn '该版本 Release 已存在，改为复用'
        $rel = Invoke-RestMethod -Uri ('https://api.github.com/repos/' + $user + '/' + $repo + '/releases/tags/v' + $Version) -Headers $hdr -TimeoutSec 30
    } else { Die ('创建 Release 失败：' + $_.Exception.Message) }
}

Step '上传发布包'
$upload = $rel.upload_url -replace '\{.*\}', ''
$upload = $upload + '?name=' + [IO.Path]::GetFileName($ZipPath)
try {
    Invoke-RestMethod -Uri $upload -Headers $hdr -Method Post -ContentType 'application/zip' -InFile $ZipPath -TimeoutSec 300 | Out-Null
    Ok ('已上传 MusicPlayer-' + $Version + '-portable.zip')
} catch {
    if ($_.Exception.Message -match '422') { Warn '同名附件已存在，跳过上传' }
    else { Warn ('上传失败：' + $_.Exception.Message) }
}

Write-Host ''
Write-Host '  全部完成！' -ForegroundColor Green
Write-Host ('  仓库：https://github.com/' + $user + '/' + $repo)
Write-Host ('  发布：https://github.com/' + $user + '/' + $repo + '/releases')
Write-Host ''
Read-Host '按回车退出'
