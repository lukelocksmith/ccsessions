# Design: tmux integration w ccsessions

Data: 2026-02-22

## Problem

Użytkownik chce podpinać się do sesji Claude po SSH lub po zamknięciu terminala.
Sesje Claude muszą być odporne na rozłączenie — działać w tle gdy użytkownik odejdzie.

## Scenariusze

- **A)** Claude działa w tle w tmuxie → użytkownik odłącza się → wraca i podpina się do tej samej sesji
- **B)** Wznowienie starej rozmowy Claude w nowej sesji tmux
- Oba scenariusze obsługiwane jednocześnie

## Design

### Zasada główna

`cs` zawsze otwiera/wznawia Claude w sesji tmux. Nie ma trybu "bez tmux" domyślnie.

### Nazewnictwo sesji tmux

Format: `claude-{nazwa-projektu}`

Przykłady: `claude-ccsessions`, `claude-writer`, `claude-important`

Nazwa projektu to ostatni segment ścieżki folderu projektu.

### Logika wyboru sesji

1. Użytkownik wybiera sesję w fzf
2. `cs` sprawdza czy istnieje tmux session o nazwie `claude-{projekt}`
3. **Jeśli tak** → `tmux attach -t claude-{projekt}`
4. **Jeśli nie** → `tmux new-session -s claude-{projekt} -c {ścieżka}` + `claude --resume {id}`

### Nowy projekt (`cn` + opcja w `cs`)

1. Tworzy folder projektu
2. Tworzy sesję tmux `claude-{nazwa}`
3. W sesji odpala `claude` (bez resume)

### Wizualizacja w fzf

Sesje z aktywnym tmuxem mają prefix `● `:

```
Claude>
● dziś 14:32    ~/Projects/ccsessions    mam tutaj konfiguracje projektu...
  dziś 09:15    ~/Projects/writer        napisz post o AI na linkedin...
  wczoraj 18:00 ~/Projects/important     chcemy postawic na orbstack...
```

### Konfiguracja

Nowy parametr w `ccsessions.conf`:

```
TMUX=true   # true/false, domyślnie true
```

Gdy `TMUX=false` — zachowanie jak dotychczas (bez zmian).

## Zmiany w kodzie

### `cs` (Python)

- `get_active_tmux_sessions()` — zwraca set nazw aktywnych sesji tmux
- `get_tmux_session_name(project_path)` — generuje nazwę `claude-{projekt}`
- `attach_or_create_tmux(session_name, project_path, claude_flags)` — attach lub new-session
- Modyfikacja `resume_session()` — używa tmux gdy `TMUX=true`
- Modyfikacja `create_new_project()` — używa tmux gdy `TMUX=true`
- Modyfikacja `format_line()` — dodaje prefix `● ` gdy sesja jest live w tmux

### `cn` (Bash)

- Czyta `TMUX` z configu
- Gdy `TMUX=true`: `tmux new-session -s claude-{nazwa} -c {ścieżka} claude {flagi}`
- Fallback: jeśli sesja tmux o tej nazwie już istnieje → attach

### `ccsessions.conf`

Dodanie `TMUX=true` jako domyślna wartość.

## Out of scope

- Zarządzanie sesjami tmux (kill, rename) — to robi `tmux` bezpośrednio
- Integracja z innymi multiplexerami (screen, zellij)
- Synchronizacja między wieloma komputerami
