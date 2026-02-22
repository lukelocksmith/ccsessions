# tmux Integration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Każda sesja Claude uruchamia się w tmuxie — sesje przeżywają rozłączenie SSH, `cs` pokazuje które są aktywne.

**Architecture:** `cs` (Python) i `cn` (Bash) opakowują wywołanie `claude` w `tmux new-session` lub `tmux attach`. Nowa funkcja `get_active_tmux_sessions()` sprawdza które sesje żyją. W fzf aktywne sesje mają prefix `●`.

**Tech Stack:** Python 3 (stdlib: subprocess, os), Bash, tmux (zakładamy zainstalowany)

---

### Task 1: Dodaj TMUX do konfiguracji i defaults

**Files:**
- Modify: `ccsessions.conf`
- Modify: `cs` (funkcja `load_config`, linia ~16)

**Step 1: Dodaj TMUX=true do ccsessions.conf**

Na końcu pliku `ccsessions.conf` dodaj:

```
# Sesje tmux (true / false)
TMUX=true
```

**Step 2: Dodaj TMUX do defaults w load_config()**

W `cs`, w słowniku `defaults` w `load_config()` dodaj:

```python
"TMUX": "true",
```

**Step 3: Sprawdź ręcznie że config się wczytuje**

```bash
python3 -c "
import sys; sys.path.insert(0, '.')
# symuluj wczytanie
defaults = {'TMUX': 'true'}
with open('ccsessions.conf') as f:
    for line in f:
        line = line.strip()
        if line and not line.startswith('#') and '=' in line:
            k, _, v = line.partition('=')
            defaults[k.strip()] = v.strip()
print(defaults.get('TMUX'))
"
```

Expected output: `true`

**Step 4: Commit**

```bash
git add ccsessions.conf cs
git commit -m "feat: add TMUX config option (default: true)"
```

---

### Task 2: Funkcje pomocnicze tmux w `cs`

**Files:**
- Modify: `cs` — dodaj 2 funkcje po `format_time()` (linia ~113)

**Step 1: Dodaj get_active_tmux_sessions()**

```python
def get_active_tmux_sessions():
    """Zwraca set nazw aktywnych sesji tmux."""
    try:
        result = subprocess.run(
            ["tmux", "list-sessions", "-F", "#{session_name}"],
            capture_output=True, text=True
        )
        if result.returncode == 0 and result.stdout.strip():
            return set(result.stdout.strip().split("\n"))
        return set()
    except FileNotFoundError:
        return set()
```

**Step 2: Dodaj get_tmux_session_name()**

```python
def get_tmux_session_name(project_path):
    """Zwraca nazwę sesji tmux dla danego projektu."""
    name = os.path.basename(project_path.rstrip("/")) or "claude"
    return f"claude-{name}"
```

**Step 3: Sprawdź ręcznie**

```bash
python3 -c "
import subprocess, os, sys; sys.path.insert(0, '.')

def get_active_tmux_sessions():
    try:
        result = subprocess.run(['tmux', 'list-sessions', '-F', '#{session_name}'],
            capture_output=True, text=True)
        if result.returncode == 0 and result.stdout.strip():
            return set(result.stdout.strip().split('\n'))
        return set()
    except FileNotFoundError:
        return set()

def get_tmux_session_name(project_path):
    name = os.path.basename(project_path.rstrip('/')) or 'claude'
    return f'claude-{name}'

print('Active tmux:', get_active_tmux_sessions())
print('Session name:', get_tmux_session_name('/Users/test/Projects/ccsessions'))
"
```

Expected: `Session name: claude-ccsessions`, active sessions zależne od systemu.

**Step 4: Commit**

```bash
git add cs
git commit -m "feat: add get_active_tmux_sessions and get_tmux_session_name helpers"
```

---

### Task 3: Funkcja attach_or_create_tmux() w `cs`

**Files:**
- Modify: `cs` — dodaj po `get_tmux_session_name()` (Task 2)

**Step 1: Dodaj funkcję**

```python
def attach_or_create_tmux(session_name, project_path, claude_flags):
    """Podpina się do istniejącej sesji tmux lub tworzy nową z claude."""
    active = get_active_tmux_sessions()
    if session_name in active:
        print(f"\n→ Podpinam się do tmux: {session_name}")
        os.execvp("tmux", ["tmux", "attach", "-t", session_name])
    else:
        print(f"\n→ Nowa sesja tmux: {session_name}")
        tmux_cmd = ["tmux", "new-session", "-s", session_name, "-c", project_path] + claude_flags
        os.execvp("tmux", tmux_cmd)
```

**Step 2: Sprawdź że funkcja jest dostępna syntaktycznie**

```bash
python3 -c "import ast; ast.parse(open('cs').read()); print('OK')"
```

Expected: `OK`

**Step 3: Commit**

```bash
git add cs
git commit -m "feat: add attach_or_create_tmux function"
```

---

### Task 4: Podpięcie tmux w resume_session()

**Files:**
- Modify: `cs` — funkcja `resume_session()` (linia ~124)

**Step 1: Zmień sygnaturę funkcji — dodaj config**

Aktualna sygnatura: `def resume_session(session, config):`
Sygnatura jest już poprawna (config przekazywany). Sprawdź wywołanie w `main()`:

```python
resume_session(sessions[idx], config)
```

Jest OK — nie trzeba zmieniać wywołania.

**Step 2: Zmodyfikuj body resume_session()**

Zastąp całe body funkcji:

```python
def resume_session(session, config):
    print(f"\n→ Projekt: {session['actual_path']}")
    print(f"→ Temat:   {session['first_msg'][:60]}")
    fork = ask_fork()
    project_path = session["actual_path"]
    if os.path.isdir(project_path):
        os.chdir(project_path)
    flags = build_claude_flags(config, resume_id=session["session_id"], fork=fork)

    if config.get("TMUX", "true").lower() == "true":
        session_name = get_tmux_session_name(project_path)
        if fork:
            # Fork = nowa rozmowa, unikalna nazwa sesji tmux
            import time
            session_name = f"{session_name}-fork-{int(time.time())}"
        attach_or_create_tmux(session_name, project_path, flags)
    else:
        os.execvp(flags[0], flags)
```

**Step 3: Sprawdź składnię**

```bash
python3 -c "import ast; ast.parse(open('cs').read()); print('OK')"
```

Expected: `OK`

**Step 4: Commit**

```bash
git add cs
git commit -m "feat: resume_session uses tmux when TMUX=true"
```

---

### Task 5: Podpięcie tmux w create_new_project()

**Files:**
- Modify: `cs` — funkcja `create_new_project()` (linia ~133)

**Step 1: Zmodyfikuj body create_new_project()**

Zastąp całe body:

```python
def create_new_project(config):
    print("\nNazwa nowego projektu: ", end="", flush=True)
    name = input().strip()
    if not name:
        sys.exit(0)
    projects_dir = os.path.expanduser(config.get("PROJECTS_DIR", "~/Projects"))
    project_path = os.path.join(projects_dir, name)
    if os.path.isdir(project_path):
        print(f"Folder już istnieje: {project_path} — wchodzę...")
    else:
        os.makedirs(project_path)
        print(f"✓ Utworzono: {project_path}")
    os.chdir(project_path)
    flags = build_claude_flags(config)

    if config.get("TMUX", "true").lower() == "true":
        session_name = get_tmux_session_name(project_path)
        attach_or_create_tmux(session_name, project_path, flags)
    else:
        os.execvp(flags[0], flags)
```

**Step 2: Sprawdź składnię**

```bash
python3 -c "import ast; ast.parse(open('cs').read()); print('OK')"
```

Expected: `OK`

**Step 3: Commit**

```bash
git add cs
git commit -m "feat: create_new_project uses tmux when TMUX=true"
```

---

### Task 6: Znacznik ● w liście fzf

**Files:**
- Modify: `cs` — funkcja `main()` (linia ~149), pętla budująca `lines`

**Step 1: Zmodyfikuj main() — pobierz aktywne sesje tmux**

W `main()`, zaraz po `home = os.path.expanduser("~")` dodaj:

```python
use_tmux = config.get("TMUX", "true").lower() == "true"
active_tmux = get_active_tmux_sessions() if use_tmux else set()
```

**Step 2: Zmodyfikuj pętlę budującą lines**

Znajdź pętlę:
```python
for i, s in enumerate(sessions):
    project = s["actual_path"].replace(home, "~")
    msg     = s["first_msg"].replace("\n", " ")
    lines.append(f"{i:04d}|{format_time(s['mtime']):<16} {project:<40} {msg}")
```

Zastąp:
```python
for i, s in enumerate(sessions):
    project      = s["actual_path"].replace(home, "~")
    msg          = s["first_msg"].replace("\n", " ")
    session_name = get_tmux_session_name(s["actual_path"])
    live         = "●" if session_name in active_tmux else " "
    lines.append(f"{i:04d}|{live} {format_time(s['mtime']):<16} {project:<40} {msg}")
```

**Step 3: Sprawdź składnię**

```bash
python3 -c "import ast; ast.parse(open('cs').read()); print('OK')"
```

Expected: `OK`

**Step 4: Commit**

```bash
git add cs
git commit -m "feat: show live tmux indicator in session list"
```

---

### Task 7: Aktualizacja cn (Bash)

**Files:**
- Modify: `cn`

**Step 1: Dodaj czytanie TMUX z configu**

W bloku `case "$key" in` (linia ~17) dodaj:

```bash
TMUX_ENABLED) TMUX_ENABLED="$val" ;;
```

Przed blokiem `case` dodaj domyślną wartość (po linii `PROJECTS_DIR="$HOME/Projects"`):

```bash
TMUX_ENABLED="true"
```

**Step 2: Zastąp ostatnie 3 linie (cd + exec claude)**

Znajdź:
```bash
cd "$PROJECT_PATH"
exec claude $CLAUDE_FLAGS
```

Zastąp:
```bash
SESSION_NAME="claude-$(basename "$PROJECT_PATH")"

if [ "$TMUX_ENABLED" = "true" ]; then
    if tmux has-session -t "$SESSION_NAME" 2>/dev/null; then
        echo "→ Podpinam się do tmux: $SESSION_NAME"
        exec tmux attach -t "$SESSION_NAME"
    else
        echo "→ Nowa sesja tmux: $SESSION_NAME"
        exec tmux new-session -s "$SESSION_NAME" -c "$PROJECT_PATH" claude $CLAUDE_FLAGS
    fi
else
    cd "$PROJECT_PATH"
    exec claude $CLAUDE_FLAGS
fi
```

**Step 3: Sprawdź składnię bash**

```bash
bash -n cn && echo "OK"
```

Expected: `OK`

**Step 4: Commit**

```bash
git add cn
git commit -m "feat: cn uses tmux when TMUX=true"
```

---

### Task 8: Aktualizacja install.sh i README

**Files:**
- Modify: `install.sh`
- Modify: `README.md`

**Step 1: Dodaj kopiowanie ccsessions.conf w install.sh**

Po linii `chmod +x ~/.local/bin/cs` dodaj:

```bash
# Kopiuj config jeśli nie istnieje
CONFIG_DIR="$HOME/.config"
mkdir -p "$CONFIG_DIR"
if [ ! -f "$CONFIG_DIR/ccsessions" ]; then
    cp "$(dirname "$0")/ccsessions.conf" "$CONFIG_DIR/ccsessions"
    echo "✓ Config skopiowany do $CONFIG_DIR/ccsessions"
else
    echo "✓ Config już istnieje: $CONFIG_DIR/ccsessions (nie nadpisano)"
fi
```

**Step 2: Dodaj sekcję tmux do README.md**

Po sekcji `## Wymagania` dodaj `tmux` do listy:

```markdown
- [tmux](https://github.com/tmux/tmux) — do trwałych sesji (opcjonalnie, wyłącz przez `TMUX=false`)
```

Po sekcji `## Użycie` dodaj nową sekcję:

```markdown
## Sesje tmux

Domyślnie każda sesja Claude jest uruchamiana w tmuxie (`TMUX=true` w configu).

**Odłącz się od sesji:** `Ctrl+B D`
**Lista sesji tmux:** `tmux ls`
**Podepnij się ręcznie:** `tmux attach -t claude-nazwa-projektu`

W `cs` sesje aktualnie otwarte w tmuxie mają prefix `●`.

Wyłącz tmux: zmień `TMUX=false` w `~/.config/ccsessions`
```

**Step 3: Sprawdź bash install.sh**

```bash
bash -n install.sh && echo "OK"
```

Expected: `OK`

**Step 4: Commit**

```bash
git add install.sh README.md
git commit -m "docs: update install.sh and README with tmux info"
```

---

### Task 9: Test manualny end-to-end

**Cel:** Potwierdzić że cały flow działa.

**Step 1: Test get_active_tmux_sessions gdy brak tmux**

```bash
python3 -c "
import subprocess, os
def get_active_tmux_sessions():
    try:
        result = subprocess.run(['tmux', 'list-sessions', '-F', '#{session_name}'],
            capture_output=True, text=True)
        if result.returncode == 0 and result.stdout.strip():
            return set(result.stdout.strip().split('\n'))
        return set()
    except FileNotFoundError:
        return set()
print('Sessions:', get_active_tmux_sessions())
"
```

Expected: `Sessions: set()` lub set z nazwami aktywnych sesji.

**Step 2: Utwórz testową sesję tmux i sprawdź detekcję**

```bash
tmux new-session -d -s claude-testowy
python3 -c "
import subprocess
result = subprocess.run(['tmux', 'list-sessions', '-F', '#{session_name}'],
    capture_output=True, text=True)
print('claude-testowy' in result.stdout)
"
tmux kill-session -t claude-testowy
```

Expected: `True`

**Step 3: Uruchom cs i sprawdź czy lista się wyświetla**

```bash
python3 cs --help 2>/dev/null || python3 cs < /dev/null 2>&1 | head -5
```

Expected: brak błędów składniowych (może pojawić się błąd fzf jeśli uruchamiamy bez terminala — to normalne).

**Step 4: Sprawdź cn**

```bash
bash -n cn && echo "cn: OK"
```

Expected: `cn: OK`
