#!/usr/bin/env bash
# Nuclear stop: deallocate the build VM. Kills all guest processes — broken PowerShell/dotnet/Playwright deploys,
# Run Command scripts, everything. Azure may take a minute to clear the Run Command slot after you start again.
#
# On your Mac: Ctrl+C only stops the redeploy *client*; it does not stop work on the VM. This does.
#
# Env: AZURE_VM_RG (default microhire-vm), AZURE_VM_NAME (default microhire-vm)
#
# After this, start the VM, then RDP and run Build-AndDeploy.ps1 or remote-redeploy.ps1 (see Scripts/azure-build-vm/README.md).
#   az vm start -g microhire-vm -n microhire-vm --no-wait
# Or: ./Scripts/azure-build-vm/hard-reboot-build-vm.sh

set -euo pipefail

VM_RG="${AZURE_VM_RG:-microhire-vm}"
VM_NAME="${AZURE_VM_NAME:-microhire-vm}"

if ! az account show &>/dev/null; then
  echo "Run: az login" >&2
  exit 1
fi

echo "Deallocating ${VM_NAME} — hard-stopping all work on the guest (broken deploys, Run Command, etc.)..."
az vm deallocate --resource-group "$VM_RG" --name "$VM_NAME"
echo ""
echo "VM is off. Bring it back, then RDP and deploy from the repo on the guest:"
echo "  az vm start -g ${VM_RG} -n ${VM_NAME} --no-wait"
echo "  # wait until VM is reachable, then on the VM:"
echo "  .\\Scripts\\azure-build-vm\\Build-AndDeploy.ps1 -RepoRoot C:\\work\\MicrohireAgentChat"
echo "Or one shot from Mac: ./Scripts/azure-build-vm/hard-reboot-build-vm.sh"
