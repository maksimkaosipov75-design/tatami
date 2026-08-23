# fzf colors sourced from theme/mocha.json - the single source of truth for
# the Catppuccin Mocha palette - instead of duplicating hex values here.
$mochaPath = Join-Path $HOME 'dotfiles\theme\mocha.json'
if (Test-Path $mochaPath) {
    $t = (Get-Content $mochaPath -Raw | ConvertFrom-Json).colors
    $env:FZF_DEFAULT_OPTS = "--color=fg:$($t.text),bg:$($t.base),hl:$($t.mauve),fg+:$($t.text),bg+:$($t.surface0),hl+:$($t.mauve),info:$($t.blue),prompt:$($t.mauve),pointer:$($t.mauve),marker:$($t.green),spinner:$($t.mauve),header:$($t.overlay0)"
}

oh-my-posh init pwsh --config (Join-Path $HOME 'dotfiles\powershell\omarchy.omp.json') | Invoke-Expression
zoxide init powershell | Out-String | Invoke-Expression

Set-PSReadLineOption -PredictionSource History -PredictionViewStyle ListView

# 'ls' and 'cat' are built-in PowerShell aliases, and aliases outrank
# same-named functions in command lookup, so they must be removed first
# or these functions would never actually get called.
Remove-Item Alias:ls -Force -ErrorAction SilentlyContinue
Remove-Item Alias:cat -Force -ErrorAction SilentlyContinue

function ls { eza --icons --git @args }
function ll { eza -la --icons --git @args }
function cat { bat --style=plain @args }
function grep { rg @args }
function find { fd @args }
