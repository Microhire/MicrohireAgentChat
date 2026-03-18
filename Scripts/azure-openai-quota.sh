#!/usr/bin/env bash
# Show Azure OpenAI usage for Australia East and open quota increase page.
# Requires: az login already done.

set -e
REGION="${AZURE_OPENAI_REGION:-australiaeast}"

echo "=== Azure OpenAI usage (region: $REGION) ==="
az cognitiveservices usage list --location "$REGION" -o table 2>/dev/null || {
  echo "Run 'az login' first. If the command failed, check your subscription and region."
  exit 1
}

echo ""
echo "Opening quota increase page in browser..."
if command -v open >/dev/null 2>&1; then
  open "https://aka.ms/oai/quotaincrease"
else
  echo "Open in your browser: https://aka.ms/oai/quotaincrease"
fi
