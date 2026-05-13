# ccsessions — Claude Sessions Manager

Narzędzie do zarządzania sesjami Claude Code ze wszystkich projektów w jednym miejscu.
Dwa skrypty: `cs` (Python) i `cn` (Bash). Instalacja przez `install.sh`.

## Pliki

| Plik | Rola |
|------|------|
| `cs` | Główny skrypt — lista sesji, wznawianie, fork |
| `cn` | Tworzenie nowego projektu i otwieranie Claude |
| `ccsessions.conf` | Szablon konfiguracji (kopiowany do `~/.config/ccsessions`) |
| `install.sh` | Instalator — kopiuje skrypty do `~/.local/bin`, config do `~/.config/ccsessions` |

## Jak działa `cs`

1. Skanuje `~/.claude/projects/{encoded-path}/*.jsonl` — każdy plik to jedna sesja Claude Code
2. Dekoduje ścieżkę projektu: `-Users-lukaszek-Projects-foo` → `/Users/lukaszek/Projects/foo`
3. Czyta pierwszą wiadomość użytkownika z każdego `.jsonl`
4. Prezentuje listę przez `fzf`
5. Po wyborze: pyta o kontynuację (`c`) lub fork (`f`), następnie odpala `claude --resume <id>` (lub `--fork-session --resume <id>`)
6. Jeśli `TMUX=true` — wszystko leci w sesji tmux `claude-<nazwa-projektu>`

### Format danych fzf

Wewnętrzny format linii: `{index:04d}|{live_marker} {czas} {projekt} {msg}`

`fzf` dostaje `--delimiter=|` z `--with-nth=2` (wyświetla tylko część po `|`) i `--nth=2`
(przeszukuje tylko tę część). Index przed `|` jest ukryty dla użytkownika, ale służy do
lookupa sesji po wyborze.

### Dekodowanie ścieżek sesji

Claude Code koduje ścieżkę projektu zamieniając `/` na `-` i dodając `-` na początku:
```
~/.claude/projects/-Users-lukaszek-Projects-writer/abc123.jsonl
                   └─ /Users/lukaszek/Projects/writer
```

Funkcja `decode_path()` w `cs` odwraca tę transformację.

## Konfiguracja

Plik: `~/.config/ccsessions`

| Klucz | Domyślnie | Opis |
|-------|-----------|------|
| `DANGEROUSLY_SKIP_PERMISSIONS` | `true` | Uruchamia claude bez promptów o uprawnienia |
| `BROWSER` | `chrome` | Rozszerzenie przeglądarki (`chrome` / `firefox` / `none`) |
| `MODEL` | `sonnet` | Model (`sonnet` / `opus` / `haiku` / pełna nazwa) |
| `EFFORT` | `high` | Poziom wysiłku (`low` / `medium` / `high`) |
| `PROJECTS_DIR` | `~/Projects` | Folder z projektami (używany przez `cn`) |
| `TMUX` | `true` | Czy otwierać sesje w tmuxie |

## tmux integration

- Nazwa sesji tmux: `claude-{basename projektu}`, np. `claude-writer`
- Jeśli sesja tmux już istnieje → `tmux attach -t {nazwa}` (nie tworzy nowej)
- Jeśli nie istnieje → `tmux new-session -s {nazwa} -c {ścieżka} claude ...`
- Fork tworzy unikalną nazwę: `claude-{projekt}-fork-{timestamp}`
- Skrypt używa `os.execvp` — zastępuje własny proces tmuxem (brak zombie-procesu)
- Wskaźnik `●` w liście `cs` oznacza sesję aktualnie otwartą w tmuxie

Odłącz się od sesji tmux bez zamykania Claude: `Ctrl+B D`

## Skrypt `cn`

Bash-owy odpowiednik dla nowych projektów:
```bash
cn nazwa-projektu   # tworzy ~/Projects/nazwa-projektu i otwiera Claude
```

Czyta te same ustawienia co `cs` z `~/.config/ccsessions`.
Buduje identyczne flagi Claude (`--dangerously-skip-permissions`, `--model`, `--effort`, itd.).

## Wymagania

- Python 3 (macOS ma domyślnie)
- `fzf` — `brew install fzf`
- `claude` (Claude Code CLI)
- `tmux` (opcjonalne, ale domyślnie włączone)

## Instalacja

```bash
./install.sh
source ~/.zshrc
cs
```

Instalator:
- kopiuje `cs` i `cn` do `~/.local/bin/`
- kopiuje `ccsessions.conf` do `~/.config/ccsessions` (nie nadpisuje istniejącego)
- dodaje `~/.local/bin` do PATH w `.zshrc` / `.bashrc` (jeśli brakuje)

## Typowe operacje

```bash
cs                    # wszystkie sesje
cs writer             # filtruj po nazwie projektu
cs "jaki ai"          # filtruj po treści pierwszej wiadomości
cn nowy-projekt       # nowy projekt + Claude w tmuxie
```

## Rozszerzanie

- Logika sesji: `list_sessions()`, `get_first_user_message()`, `decode_path()` w `cs`
- Logika tmux: `attach_or_create_tmux()`, `get_active_tmux_sessions()` w `cs`
- Nowe flagi Claude: `build_claude_flags()` w `cs` i analogiczna sekcja w `cn`
- Nowe opcje config: `load_config()` w `cs` i blok `case` w `cn`
