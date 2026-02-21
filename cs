#!/usr/bin/env python3
"""
cs — Claude Sessions manager
Wznawia sesje Claude lub tworzy nowy projekt.
Użycie: cs [opcjonalny filtr]
"""

import json, os, sys, subprocess, glob
from datetime import datetime

SESSIONS_DIR       = os.path.expanduser("~/.claude/projects")
CONFIG_FILE        = os.path.expanduser("~/.config/ccsessions")
NEW_PROJECT_MARKER = "NEW|+ Nowy projekt"

def load_config():
    defaults = {
        "DANGEROUSLY_SKIP_PERMISSIONS": "true",
        "BROWSER":      "chrome",
        "MODEL":        "sonnet",
        "EFFORT":       "high",
        "PROJECTS_DIR": "~/Projects",
    }
    if os.path.exists(CONFIG_FILE):
        with open(CONFIG_FILE) as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith("#") and "=" in line:
                    key, _, val = line.partition("=")
                    defaults[key.strip()] = val.strip()
    return defaults

def build_claude_flags(config, resume_id=None, fork=False):
    flags = ["claude"]
    if config.get("DANGEROUSLY_SKIP_PERMISSIONS", "true").lower() == "true":
        flags.append("--dangerously-skip-permissions")
    browser = config.get("BROWSER", "chrome").lower()
    if browser not in ("none", ""):
        flags.append(f"--{browser}")
    model = config.get("MODEL", "sonnet")
    if model not in ("none", ""):
        flags += ["--model", model]
    effort = config.get("EFFORT", "high")
    if effort not in ("none", ""):
        flags += ["--effort", effort]
    if resume_id:
        if fork:
            flags.append("--fork-session")
        flags += ["--resume", resume_id]
    return flags

def decode_path(project_dir):
    if project_dir.startswith("-"):
        return "/" + project_dir[1:].replace("-", "/")
    return project_dir

def get_first_user_message(jsonl_file):
    try:
        with open(jsonl_file, "r") as f:
            for line in f:
                try:
                    entry = json.loads(line)
                    if entry.get("type") == "user" and entry.get("message", {}).get("role") == "user":
                        content = entry["message"].get("content", "")
                        if isinstance(content, list):
                            for c in content:
                                if isinstance(c, dict) and c.get("type") == "text":
                                    txt = c["text"].strip()
                                    if txt and not txt.startswith("<"):
                                        return txt[:80]
                        elif isinstance(content, str) and content.strip() and not content.startswith("<"):
                            return content.strip()[:80]
                except Exception:
                    pass
        return ""
    except Exception:
        return ""

def list_sessions(filter_str=""):
    sessions = []
    for project_dir in os.listdir(SESSIONS_DIR):
        project_path = os.path.join(SESSIONS_DIR, project_dir)
        if not os.path.isdir(project_path):
            continue
        actual_path = decode_path(project_dir)
        for jsonl_file in glob.glob(os.path.join(project_path, "*.jsonl")):
            try:
                if os.path.getsize(jsonl_file) < 100:
                    continue
                first_msg = get_first_user_message(jsonl_file)
                if not first_msg:
                    continue
                sessions.append({
                    "actual_path": actual_path,
                    "session_id":  os.path.basename(jsonl_file).replace(".jsonl", ""),
                    "mtime":       os.path.getmtime(jsonl_file),
                    "first_msg":   first_msg,
                })
            except:
                pass
    sessions.sort(key=lambda x: x["mtime"], reverse=True)
    if filter_str:
        fl = filter_str.lower()
        sessions = [s for s in sessions
                    if fl in s["actual_path"].lower() or fl in s["first_msg"].lower()]
    return sessions

def format_time(mtime):
    dt   = datetime.fromtimestamp(mtime)
    diff = datetime.now() - dt
    if diff.days == 0:  return dt.strftime("dziś %H:%M")
    if diff.days == 1:  return dt.strftime("wczoraj %H:%M")
    if diff.days < 7:   return dt.strftime("%A %H:%M")
    return dt.strftime("%d.%m.%Y")

def ask_fork():
    """Pyta czy wznowić sesję czy zrobić fork (nową sesję od tego punktu)."""
    print("\n[c] kontynuuj sesję  [f] fork (nowa sesja od tego miejsca): ", end="", flush=True)
    try:
        choice = input().strip().lower()
        return choice == "f"
    except (EOFError, KeyboardInterrupt):
        return False

def resume_session(session, config):
    print(f"\n→ Projekt: {session['actual_path']}")
    print(f"→ Temat:   {session['first_msg'][:60]}")
    fork = ask_fork()
    if os.path.isdir(session["actual_path"]):
        os.chdir(session["actual_path"])
    flags = build_claude_flags(config, resume_id=session["session_id"], fork=fork)
    os.execvp(flags[0], flags)

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
    os.execvp(flags[0], flags)

def main():
    config     = load_config()
    filter_str = " ".join(sys.argv[1:]) if len(sys.argv) > 1 else ""
    sessions   = list_sessions(filter_str)
    home       = os.path.expanduser("~")

    lines = []
    if not filter_str:
        lines.append(NEW_PROJECT_MARKER)
    for i, s in enumerate(sessions):
        project = s["actual_path"].replace(home, "~")
        msg     = s["first_msg"].replace("\n", " ")
        lines.append(f"{i:04d}|{format_time(s['mtime']):<16} {project:<40} {msg}")

    if not lines:
        print("Brak sesji" + (f" pasujących do '{filter_str}'" if filter_str else ""))
        sys.exit(0)

    fzf_bin = "/opt/homebrew/bin/fzf"
    if not os.path.exists(fzf_bin):
        fzf_bin = "fzf"

    try:
        result = subprocess.run(
            [fzf_bin, "--height=60%", "--reverse", "--border",
             "--prompt=Claude> ",
             "--header=ENTER: otwórz  ESC: anuluj",
             "--delimiter=|", "--nth=2", "--with-nth=2"],
            input="\n".join(lines),
            capture_output=True, text=True
        )
    except FileNotFoundError:
        print("Zainstaluj fzf: brew install fzf")
        sys.exit(1)

    if result.returncode != 0:
        sys.exit(0)

    selected = result.stdout.strip()
    if not selected:
        sys.exit(0)

    if selected == NEW_PROJECT_MARKER:
        create_new_project(config)
        return

    try:
        idx = int(selected.split("|")[0])
        resume_session(sessions[idx], config)
    except (ValueError, IndexError):
        sys.exit(0)

if __name__ == "__main__":
    main()
