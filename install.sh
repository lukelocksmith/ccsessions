#!/bin/bash
# Instalator cs — Claude Sessions Manager

set -e

echo "=== Instalacja cs — Claude Sessions Manager ==="
echo ""

# Sprawdź fzf
if ! command -v fzf &>/dev/null; then
    echo "Instaluję fzf..."
    if command -v brew &>/dev/null; then
        brew install fzf
    else
        echo "Zainstaluj fzf ręcznie: https://github.com/junegunn/fzf"
        exit 1
    fi
fi

# Skopiuj skrypt
mkdir -p ~/.local/bin
cp "$(dirname "$0")/cs" ~/.local/bin/cs
chmod +x ~/.local/bin/cs
echo "✓ Skrypt skopiowany do ~/.local/bin/cs"

cp "$(dirname "$0")/cn" ~/.local/bin/cn
chmod +x ~/.local/bin/cn
echo "✓ Skrypt cn skopiowany do ~/.local/bin/cn"

# Kopiuj config jeśli nie istnieje
CONFIG_DIR="$HOME/.config"
mkdir -p "$CONFIG_DIR"
if [ ! -f "$CONFIG_DIR/ccsessions" ]; then
    cp "$(dirname "$0")/ccsessions.conf" "$CONFIG_DIR/ccsessions"
    echo "✓ Config skopiowany do $CONFIG_DIR/ccsessions"
else
    echo "✓ Config już istnieje: $CONFIG_DIR/ccsessions (nie nadpisano)"
fi

# Dodaj do PATH jeśli nie ma
SHELL_RC="$HOME/.zshrc"
if [ -n "$BASH_VERSION" ]; then
    SHELL_RC="$HOME/.bashrc"
fi

if ! grep -q 'HOME/.local/bin' "$SHELL_RC" 2>/dev/null; then
    echo 'export PATH="$HOME/.local/bin:$PATH"' >> "$SHELL_RC"
    echo "✓ Dodano PATH do $SHELL_RC"
fi

echo ""
echo "=== Gotowe! ==="
echo ""
echo "Uruchom: source $SHELL_RC"
echo "Potem:   cs"
