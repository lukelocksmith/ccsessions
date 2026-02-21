#!/usr/bin/env python3
"""
cs — Claude Sessions manager
Wyświetla wszystkie sesje Claude ze wszystkich projektów i pozwala je wznowić.
Użycie: cs [opcjonalny filtr]
"""

import json, os, sys, subprocess, glob, re
from datetime import datetime

PROJECTS_DIR = os.path.expanduser("~/.claude/projects")

def decode_path(project_dir):
    """Zamień zakodowaną nazwę folderu na ścieżkę np. -Users-lukaszek-Projects-writer -> /Users/lukaszek/Projects/writer"""
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

def get_message_count(jsonl_file):
    try:
        count = 0
        with open(jsonl_file, "r") as f:
            for line in f:
                try:
                    entry = json.loads(line)
                    if entry.get("type") in ("user", "assistant"):
                        count += 1
                except:
                    pass
        return count
    except:
        return 0

def list_sessions(filter_str=""):
    sessions = []

    for project_dir in os.listdir(PROJECTS_DIR):
        project_path = os.path.join(PROJECTS_DIR, project_dir)
        if not os.path.isdir(project_path):
            continue

        actual_path = decode_path(project_dir)
        project_name = os.path.basename(actual_path)

        for jsonl_file in glob.glob(os.path.join(project_path, "*.jsonl")):
            try:
                mtime = os.path.getmtime(jsonl_file)
                size = os.path.getsize(jsonl_file)
                if size < 100:
                    continue  # pomiń puste sesje

                session_id = os.path.basename(jsonl_file).replace(".jsonl", "")
                first_msg = get_first_user_message(jsonl_file)

                if not first_msg:
                    continue  # pomiń sesje bez wiadomości

                sessions.append({
                    "project_dir": project_dir,
                    "actual_path": actual_path,
                    "project_name": project_name,
                    "session_id": session_id,
                    "mtime": mtime,
                    "first_msg": first_msg,
                })
            except:
                pass

    sessions.sort(key=lambda x: x["mtime"], reverse=True)

    if filter_str:
        fl = filter_str.lower()
        sessions = [s for s in sessions if fl in s["actual_path"].lower() or fl in s["first_msg"].lower()]

    return sessions

def format_time(mtime):
    dt = datetime.fromtimestamp(mtime)
    now = datetime.now()
    diff = now - dt
    if diff.days == 0:
        return dt.strftime("dziś %H:%M")
    elif diff.days == 1:
        return dt.strftime("wczoraj %H:%M")
    elif diff.days < 7:
        return dt.strftime("%A %H:%M")
    else:
        return dt.strftime("%d.%m.%Y")

def main():
    filter_str = " ".join(sys.argv[1:]) if len(sys.argv) > 1 else ""

    sessions = list_sessions(filter_str)

    if not sessions:
        print("Brak sesji" + (f" pasujących do '{filter_str}'" if filter_str else ""))
        sys.exit(0)

    # Buduj linie dla fzf
    lines = []
    for s in sessions:
        time_str = format_time(s["mtime"])
        project = s["actual_path"].replace("/Users/lukaszek/", "~/").replace("/Users/lukaszek", "~")
        msg = s["first_msg"].replace("\n", " ")
        line = f"{time_str:<16} {project:<40} {msg}"
        lines.append(line)

    fzf_input = "\n".join(lines)

    try:
        result = subprocess.run(
            ["/opt/homebrew/bin/fzf",
             "--ansi",
             "--height=60%",
             "--reverse",
             "--border",
             "--prompt=Claude session> ",
             "--header=ENTER: wznów sesję  ESC: anuluj",
             "--preview-window=hidden"],
            input=fzf_input,
            capture_output=True,
            text=True
        )
    except FileNotFoundError:
        # fzf nie znaleziony - wypisz listę numerowaną
        for i, (s, line) in enumerate(zip(sessions, lines)):
            print(f"  {i+1:2}. {line}")
        print(f"\nWybierz numer (1-{len(sessions)}): ", end="")
        try:
            choice = int(input()) - 1
            selected = sessions[choice]
        except:
            sys.exit(0)
        resume_session(selected)
        return

    if result.returncode != 0:
        sys.exit(0)

    selected_line = result.stdout.strip()
    if not selected_line:
        sys.exit(0)

    # Znajdź pasującą sesję
    for i, line in enumerate(lines):
        if line == selected_line:
            resume_session(sessions[i])
            return

def resume_session(session):
    project_path = session["actual_path"]
    session_id = session["session_id"]

    print(f"\n→ Projekt:  {project_path}")
    print(f"→ Sesja:    {session_id}")
    print(f"→ Temat:    {session['first_msg'][:60]}")
    print()

    # Sprawdź czy folder istnieje
    if not os.path.isdir(project_path):
        print(f"Folder nie istnieje: {project_path}")
        print(f"Uruchamiam claude --resume z bieżącego folderu...")
        os.execvp("claude", ["claude", "--resume", session_id])
    else:
        # cd do folderu i uruchom claude --resume
        os.chdir(project_path)
        os.execvp("claude", ["claude", "--resume", session_id])

if __name__ == "__main__":
    main()
