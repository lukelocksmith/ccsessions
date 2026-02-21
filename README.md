# cs — Claude Sessions Manager

Skrypt do zarządzania sesjami Claude Code ze wszystkich projektów w jednym miejscu.

## Co robi?

`cs` skanuje wszystkie sesje Claude Code na Twoim komputerze (z każdego folderu/projektu)
i pokazuje je w interaktywnej liście. Wybierasz sesję → otwiera się Claude w odpowiednim
folderze z wznowioną rozmową.

## Wymagania

- Python 3 (domyślnie na macOS)
- [fzf](https://github.com/junegunn/fzf) — do interaktywnego wyboru sesji
- Claude Code CLI (`claude`)

## Instalacja

### 1. Zainstaluj fzf

```bash
brew install fzf
```

### 2. Skopiuj skrypt

```bash
mkdir -p ~/.local/bin
cp cs ~/.local/bin/cs
chmod +x ~/.local/bin/cs
```

### 3. Dodaj do PATH

Dodaj do `~/.zshrc` (lub `~/.bashrc`):

```bash
export PATH="$HOME/.local/bin:$PATH"
```

Przeładuj terminal:

```bash
source ~/.zshrc
```

## Użycie

```bash
cs                    # wszystkie sesje ze wszystkich projektów
cs writer             # filtruj po nazwie folderu/projektu
cs "jaki ai"          # filtruj po treści pierwszej wiadomości
```

### Jak to działa?

1. Uruchamiasz `cs` z dowolnego miejsca w terminalu
2. Widzisz listę sesji: data, folder projektu, pierwsza wiadomość
3. Strzałki góra/dół do nawigacji, `Enter` żeby wybrać
4. Claude otwiera się w katalogu projektu z wznowioną sesją (`claude --resume <id>`)
5. `ESC` — wyjście bez wyboru

### Przykładowy widok

```
Claude session>
dziś 22:31     ~/Projects/writer          jaki ai jest najlepszy zeby robic...
dziś 16:40     ~/Projects/linkedin        chce z terminala wrzucac posty na...
wczoraj 15:46  ~/Projects/important       dobra chcemy postawic na orbstack...
20.02.2026     ~/Projects/kongresftb      znajdz maile od mariusza debskiego...
```

## Jak działa pod spodem?

Sesje Claude Code są zapisane jako pliki `.jsonl` w:
```
~/.claude/projects/{zakodowana-ścieżka}/{session-id}.jsonl
```

Skrypt dekoduje ścieżki, czyta pierwsze wiadomości i sortuje po czasie modyfikacji.

## Autor

Łukasz Ślusarski / important.is
