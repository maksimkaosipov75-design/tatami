local wezterm = require('wezterm')
local config = wezterm.config_builder()

-- WezTerm ships an authentic built-in 'Catppuccin Mocha' scheme (the same
-- published hex values as theme/mocha.json). Re-deriving a full ANSI
-- palette from our deliberately-partial 10-color mocha.json would mean
-- inventing colors (cyan/magenta/etc.) that aren't in that file at all, so
-- this references the palette by name instead of duplicating hex values.
config.color_scheme = 'Catppuccin Mocha'

config.font = wezterm.font('JetBrainsMono Nerd Font')
config.font_size = 11

config.enable_tab_bar = false
config.window_decorations = 'RESIZE'
config.window_padding = {
  left = 8,
  right = 8,
  top = 8,
  bottom = 8,
}

config.default_prog = { 'pwsh.exe', '-NoLogo' }

return config
