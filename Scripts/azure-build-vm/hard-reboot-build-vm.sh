#!/usr/bin/env bash
# Full stop + start (deallocate → start). Stronger than `az vm restart`; clears wedged Run Command extension.
# Kills all guest processes; may leave partial build artifacts under the repo on the data disk.
#
# Env: AZURE_VM_RG (default: microhire-vm), AZURE_VM_NAME (default: microhire-vm)
#
# After start, RDP to the VM and run Build-AndDeploy.ps1 or remote-redeploy.ps1 (see Scripts/azure-build-vm/README.md).

set -euo pipefail

VM_RG="${AZURE_VM_RG:-microhire-vm}"
VM_NAME="${AZURE_VM_NAME:-microhire-vm}"

if ! az account show &>/dev/null; then
  echo "Run: az login" >&2
  exit 1
fi

echo "=== Hard reset: deallocate ${VM_NAME} (RG ${VM_RG}) ==="
az vm deallocate --resource-group "$VM_RG" --name "$VM_NAME"

echo "=== Start ${VM_NAME} (no-wait — avoids blocking spinner; start still runs in Azure) ==="
az vm start --resource-group "$VM_RG" --name "$VM_NAME" --no-wait

echo "=== Waiting until PowerState/running (up to ~10 min) ==="
for _ in $(seq 1 40); do
  code=$(az vm get-instance-view -g "$VM_RG" -n "$VM_NAME" \
    --query "instanceView.statuses[?starts_with(code, 'PowerState/')].code" -o tsv 2>/dev/null | head -1)
  if [[ "$code" == "PowerState/running" ]]; then
    echo "PowerState/running"
    break
  fi
  echo "  ... ${code:-waiting}"
  sleep 15
done

echo ""
echo "Done. RDP to the VM, then from repo root (e.g. C:\\work\\MicrohireAgentChat):"
echo "  .\\Scripts\\azure-build-vm\\Build-AndDeploy.ps1 -RepoRoot (Get-Location).Path"
echo "Or pull + build: .\\Scripts\\azure-build-vm\\remote-redeploy.ps1"
