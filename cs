#!/usr/bin/env python3
"""
cs — Claude Sessions manager
Wznawia sesje Claude lub tworzy nowy projekt.
Użycie: cs [opcjonalny filtr]
"""

import json, os, sys, subprocess, glob
from datetime import datetime

SESSIONS_DIR = os.path.expanduser("~/.claude/projects")
PROJECTS_DIR = os.path.expanduser("~/Projects")
NEW_PROJECT_MARKER = "NEW|+ Nowy projekt"

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
                    "session_id": os.path.basename(jsonl_file).replace(".jsonl", ""),
                    "mtime": os.path.getmtime(jsonl_file),
                    "first_msg": first_msg,
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
    dt = datetime.fromtimestamp(mtime)
    diff = datetime.now() - dt
    if diff.days == 0:    return dt.strftime("dziś %H:%M")
    elif diff.days == 1:  return dt.strftime("wczoraj %H:%M")
    elif diff.days < 7:   return dt.strftime("%A %H:%M")
    return dt.strftime("%d.%m.%Y")

def resume_session(session):
    print(f"\n→ Projekt: {session['actual_path']}")
    print(f"→ Temat:   {session['first_msg'][:60]}\n")
    if os.path.isdir(session["actual_path"]):
        os.chdir(session["actual_path"])
    os.execvp("claude", ["claude", "--resume", session["session_id"]])

def create_new_project():
    print("\nNazwa nowego projektu: ", end="", flush=True)
    name = input().strip()
    if not name:
        sys.exit(0)
    project_path = os.path.join(PROJECTS_DIR, name)
    if os.path.isdir(project_path):
        print(f"Folder już istnieje: {project_path} — wchodzę...")
    else:
        os.makedirs(project_path)
        print(f"✓ Utworzono: {project_path}")
    os.chdir(project_path)
    os.execvp("claude", ["claude", "--dangerously-skip-permissions", "--chrome"])

def run_fzf(lines, fzf_bin):
    return subprocess.run(
        [fzf_bin,
         "--height=60%",
         "--reverse",
         "--border",
         "--prompt=Claude> ",
         "--header=ENTER: otwórz  ESC: anuluj",
         "--delimiter=|",
         "--nth=2",
         "--with-nth=2"],
        input="\n".join(lines),
        capture_output=True,
        text=True
    )

def main():
    filter_str = " ".join(sys.argv[1:]) if len(sys.argv) > 1 else ""
    sessions = list_sessions(filter_str)
    home = os.path.expanduser("~")

    # Buduj linie: indeks|wyświetlana treść
    # Nowy projekt zawsze na górze (jeśli brak filtra)
    lines = []
    if not filter_str:
        lines.append(NEW_PROJECT_MARKER)

    for i, s in enumerate(sessions):
        project = s["actual_path"].replace(home, "~")
        msg = s["first_msg"].replace("\n", " ")
        lines.append(f"{i:04d}|{format_time(s['mtime']):<16} {project:<40} {msg}")

    if not lines:
        print("Brak sesji" + (f" pasujących do '{filter_str}'" if filter_str else ""))
        sys.exit(0)

    fzf_bin = "/opt/homebrew/bin/fzf"
    if not os.path.exists(fzf_bin):
        fzf_bin = "fzf"

    try:
        result = run_fzf(lines, fzf_bin)
    except FileNotFoundError:
        print("Zainstaluj fzf: brew install fzf")
        sys.exit(1)

    if result.returncode != 0:
        sys.exit(0)

    selected = result.stdout.strip()
    if not selected:
        sys.exit(0)

    if selected == NEW_PROJECT_MARKER:
        create_new_project()
        return

    try:
        idx = int(selected.split("|")[0])
        resume_session(sessions[idx])
    except (ValueError, IndexError):
        sys.exit(0)

if __name__ == "__main__":
    main()
