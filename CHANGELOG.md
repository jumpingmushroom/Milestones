# Changelog

## 0.1.0 — first cut

- The achievement details panel shows every objective with a bar, `current / total` and a
  percentage instead of `??? / ???`, plus an overall bar per achievement (objectives met for
  "do everything once" achievements, average fill otherwise, the game's own 1.15x rule for the
  lenient build achievements). Unfinished objectives sort to the top. Achievements that require
  other achievements now list them.
- Pin up to three achievements per character from the details panel (X on a gamepad). A HUD
  tracker under the status-effect icons shows them and moves as you play. Large achievements
  show the four unfinished objectives closest to done. Unlocked pins clear themselves after ten
  seconds.
- Toasts in the game's message feed: each completed objective of a pinned achievement, and
  25 / 50 / 75 % for every achievement.
- When the game has stopped counting achievement stats (cheats, cheated items, world
  modifiers, or another mod setting `Game.isModded`), the tracker says so.
- `milestones`, `milestones <name>`, `milestones pin|unpin <name>`, `milestones ui` console
  commands.
