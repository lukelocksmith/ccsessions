# ccsessions — Claude Code Session Manager

Browse and resume Claude Code sessions from all your projects in one place.

## What it does

`cs` scans all Claude Code sessions on your machine (across every project folder) and shows them in an interactive list. Pick a session → Claude reopens in the right directory with the conversation resumed.

**Search works across the full conversation** — not just the first message. Type `test` and it finds every session where that word appeared at any point, ordered by date.

```
Claude> test
  dziś 22:22    …/important/testsabsplit   znajdz mi wszystkie informacje na...
  dziś 22:18    ~/Projects/api             napisz testy jednostkowe dla endp...
  28.04.2026    ~/Projects/wdf             gdzies robilismy testy przez grafa...
```

## Requirements

- Python 3 (built-in on macOS)
- [fzf](https://github.com/junegunn/fzf) — interactive picker
- [Claude Code CLI](https://docs.anthropic.com/en/docs/claude-code) (`claude`)
- [tmux](https://github.com/tmux/tmux) — persistent sessions (optional, disable with `TMUX=false`)

## Install

```bash
brew install fzf
git clone https://github.com/lukelocksmith/ccsessions
cd ccsessions
./install.sh
source ~/.zshrc
```

The installer copies `cs` and `cn` to `~/.local/bin` and the config to `~/.config/ccsessions`.

## Usage

```bash
cs              # all sessions from all projects
cs writer       # filter by project folder name
cs "migration"  # filter by anything said in the conversation
```

### Inside the picker

| Key | Action |
|-----|--------|
| `↑ ↓` | Navigate |
| `Enter` | Open session |
| `ESC` | Quit |

After selecting a session:
- **`c`** — continue (resume the session)
- **`f`** — fork (start a new session branching from this point)

### Create a new project

```bash
cn my-project   # creates ~/Projects/my-project and opens Claude
```

Or press `Enter` on **+ New project** at the top of the `cs` list.

## tmux

Sessions open in tmux by default. Each project gets a persistent tmux session named `claude-<project>`.

| Command | Action |
|---------|--------|
| `Ctrl+B D` | Detach (keep session running) |
| `tmux ls` | List all tmux sessions |
| `tmux attach -t claude-myproject` | Reattach manually |

Sessions currently open in tmux show a `●` prefix in the list.

Disable tmux: set `TMUX=false` in `~/.config/ccsessions`.

## Configuration

`~/.config/ccsessions`:

```ini
DANGEROUSLY_SKIP_PERMISSIONS=true   # skip Claude permission prompts
BROWSER=chrome                       # browser extension (chrome/firefox/none)
MODEL=sonnet                         # claude model
EFFORT=high                          # effort level (low/medium/high)
PROJECTS_DIR=~/Projects              # where cn creates new projects
TMUX=true                            # use tmux for sessions
```

## How it works

Claude Code stores sessions as `.jsonl` files:
```
~/.claude/projects/{encoded-path}/{session-id}.jsonl
```

`cs` decodes the paths, reads up to 30 user messages per session, and presents them sorted newest-first. Search is exact substring matching across all indexed messages — results are ordered by date, with path matches ranked above content-only matches.

## Author

Łukasz Ślusarski / [important.is](https://important.is)
