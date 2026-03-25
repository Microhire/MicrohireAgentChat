#!/usr/bin/env bash
# Example: create a Windows build VM + NSG (RDP from your IP only) + system-assigned managed identity.
# Replace placeholders, then run from a machine with Azure CLI logged in (bash/macOS/Linux/WSL).
# Do NOT commit secrets. Adjust SKU/region as needed.
#
# Prerequisites: az login, correct subscription set (az account set --subscription ...)

set -euo pipefail

SUBSCRIPTION_ID="${SUBSCRIPTION_ID:-}"          # optional: az account show --query id -o tsv
LOCATION="${LOCATION:-australiasoutheast}"
RG_VM="${RG_VM:-rg-microhire-build}"
VM_NAME="${VM_NAME:-vm-microhire-winbuild}"
VM_IMAGE="${VM_IMAGE:-MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest}"
VM_SIZE="${VM_SIZE:-Standard_D2s_v5}"
ADMIN_USER="${ADMIN_USER:-azureuser}"
VM_ADMIN_PASSWORD="${VM_ADMIN_PASSWORD:-}"
VNET_NAME="${VNET_NAME:-vnet-build}"
SUBNET_NAME="${SUBNET_NAME:-snet-build}"
NSG_NAME="${NSG_NAME:-nsg-build}"
APP_RG="${APP_RG:-rg-JennyJunkeer-9509}"        # App Service resource group (for RBAC)
WEBAPP_NAME="${WEBAPP_NAME:-microhire}"

if [[ -z "${MY_IP:-}" ]]; then
  echo "Set MY_IP to your public IP (e.g. export MY_IP=\$(curl -s ifconfig.me))"
  exit 1
fi

if [[ -z "$VM_ADMIN_PASSWORD" ]]; then
  echo "Set VM_ADMIN_PASSWORD to a strong Windows admin password for ${ADMIN_USER} (not committed)."
  exit 1
fi

if [[ -n "$SUBSCRIPTION_ID" ]]; then
  az account set --subscription "$SUBSCRIPTION_ID"
fi

az group create --name "$RG_VM" --location "$LOCATION"

az network nsg create --resource-group "$RG_VM" --name "$NSG_NAME" --location "$LOCATION"
az network nsg rule create \
  --resource-group "$RG_VM" \
  --nsg-name "$NSG_NAME" \
  --name AllowRdpFromMyIp \
  --priority 1000 \
  --source-address-prefixes "$MY_IP" \
  --source-port-ranges '*' \
  --destination-address-prefixes '*' \
  --destination-port-ranges 3389 \
  --access Allow \
  --protocol Tcp \
  --description "RDP from operator IP only"

az network vnet create \
  --resource-group "$RG_VM" \
  --name "$VNET_NAME" \
  --address-prefixes 10.60.0.0/16 \
  --subnet-name "$SUBNET_NAME" \
  --subnet-prefixes 10.60.1.0/24 \
  --network-security-group "$NSG_NAME"

az vm create \
  --resource-group "$RG_VM" \
  --name "$VM_NAME" \
  --image "$VM_IMAGE" \
  --size "$VM_SIZE" \
  --admin-username "$ADMIN_USER" \
  --admin-password "$VM_ADMIN_PASSWORD" \
  --assign-identity \
  --public-ip-sku Standard \
  --vnet-name "$VNET_NAME" \
  --subnet "$SUBNET_NAME"

az vm get-instance-view -g "$RG_VM" -n "$VM_NAME" -o table

PRINCIPAL_ID=$(az vm show -g "$RG_VM" -n "$VM_NAME" --query identity.principalId -o tsv)
SCOPE=$(az group show -n "$APP_RG" --query id -o tsv)

echo "Granting VM identity permission to deploy to resource group $APP_RG ..."
az role assignment create \
  --assignee "$PRINCIPAL_ID" \
  --role "Website Contributor" \
  --scope "$SCOPE" \
  || echo "If assignment fails, try --role Contributor on the same scope, or assign at the web app resource scope."

echo "RDP to the VM public IP, run Install-BuildVmToolchain.ps1, copy or clone the repo, then Build-AndDeploy.ps1"
echo "On the VM: az login --identity"
