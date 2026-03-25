# Azure Windows VM: build runner for App Service (same URL)

Use a **Windows VM in Azure** to produce `site.zip` (win-x64 + Playwright Chromium in `pw-browsers`) and deploy to the existing App Service **`microhire`**. The public **`*.azurewebsites.net` URL stays on App Service**; the VM is only a substitute when GitHub Actions or local Mac/Linux publish is not available.

Flow matches [.github/workflows/publish-windows-azure.yml](../../.github/workflows/publish-windows-azure.yml).

## 1. How to get source onto the VM (pick one)

| Method | When to use |
|--------|-------------|
| **Git clone** | You have a GitHub PAT or SSH key with **read** access to the repo (often easier to obtain than org-wide OAuth). |
| **Copy zip / RDP paste** | Repo on your laptop; zip the folder (without `bin`/`obj` is fine), copy to VM, unzip. No GitHub from VM. |
| **Azure DevOps** | Mirror the repo to Azure Repos; clone from DevOps on the VM to bypass GitHub entirely for CI. |

### Step-by-step: get the repo onto the VM, then deploy (like `deploy.sh`)

Replace `YOUR_VM_IP` with the VM’s **public IP** (Portal → VM → Overview). Use the **Windows admin account** you created with the VM.

#### Option A — Easiest on a Windows VM: RDP + shared folder (no SCP)

**On your Mac**

1. Install **Microsoft Remote Desktop** from the App Store (if you don’t have it).
2. Add a PC: **PC name** = `YOUR_VM_IP`, **User account** = your VM admin (e.g. `.\microhire` or `AzureAD\user` depending on how you created the VM).
3. **Edit** that PC → **Folders** → **+** → choose a folder that contains your project (e.g. the parent of `MicrohireAgentChat`).
4. **Start** the session and complete login.

**On the VM (inside RDP)**

5. Open **File Explorer**. In the address bar enter: `\\tsclient\` — you should see your shared folder name. Open it and confirm you see the `MicrohireAgentChat` folder (repo root with `MicrohireAgentChat\MicrohireAgentChat.csproj` inside).
6. Optional: copy the folder to a local path, e.g. `C:\work\MicrohireAgentChat`, so builds are faster (right‑click → Copy → paste under `C:\work`).

**One-time setup**

7. Open **PowerShell as Administrator** on the VM:

   ```powershell
   Set-ExecutionPolicy -Scope CurrentUser RemoteSigned -Force
   cd C:\work\MicrohireAgentChat\Scripts\azure-build-vm
   .\Install-BuildVmToolchain.ps1
   ```

8. Close PowerShell, open a **new** PowerShell (normal is fine), then sign in to Azure:

   ```powershell
   az login
   ```

   Complete the browser/device login with an account that can run `az webapp deploy` to **`microhire`** in **`rg-JennyJunkeer-9509`** (same rights you use with `./deploy.sh` on your Mac).

**Build + deploy (same outcome as CI / `deploy.sh` with Windows Chromium)**

9. From repo root on the VM:

   ```powershell
   cd C:\work\MicrohireAgentChat
   .\Scripts\azure-build-vm\Build-AndDeploy.ps1 -RepoRoot (Get-Location).Path
   ```

That publishes **win-x64**, runs **Playwright Chromium** into **`pw-browsers`**, creates **`site.zip`**, and runs **`az webapp deploy`** to **`microhire`**.

**Packaging:** The script zips with **`ZipFile.CreateFromDirectory`** (not `Compress-Archive` with a `*` wildcard). On Windows PowerShell, wildcards **skip dot-prefixed folders**, so **`.playwright`** (the Playwright Node driver) would be **omitted** from `site.zip` and PDF generation would fail on Azure with “driver not found”. If you zip manually, use the same API or include `.playwright` explicitly.

**Git `pull` on Windows (`filename too long`):** Azure log extracts under `_azure-logs/` or `_azure-log-extract/` must **not** be committed — some Kudu trace file names exceed Windows path limits and break checkout. The repo ignores those folders. If `git pull` still fails: delete any local `_azure-logs` / `_azure-log-extract` folders, run `git config --global core.longpaths true`, then `git pull` again (or `git fetch` + `git reset --hard origin/master` only if you have no local commits you need).

**Stuck build / kill and retry (RDP on the VM):** Open **Task Manager** (Ctrl+Shift+Esc) → **Details** → end stray **`dotnet.exe`** / **`pwsh.exe`** / **`powershell.exe`** rows tied to your build (or close the PowerShell window running the script). Then open a new PowerShell at the repo root and run **`Build-AndDeploy.ps1`** again. Alternatively, **`git pull`** then **`.\\Scripts\\azure-build-vm\\remote-redeploy.ps1`** (pull + full build/deploy).

- Build only (no deploy): add **`-SkipDeploy`**, then deploy the zip yourself later.
- If `az webapp deploy` says forbidden: your Azure user needs **Contributor** (or equivalent) on **`rg-JennyJunkeer-9509`**, or use a VM **managed identity** + role assignment (see §2 / [provision-build-vm.example.sh](./provision-build-vm.example.sh)).

#### Option B — `scp` from Mac (after OpenSSH Server is on the VM)

Windows does not ship with SSH server enabled. **On the VM (Administrator PowerShell)**:

```powershell
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
Start-Service sshd
Set-Service -Name sshd -StartupType Automatic
New-NetFirewallRule -Name sshd -DisplayName "OpenSSH Server (sshd)" -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22
```

**In Azure Portal:** VM → **Networking** → add an inbound rule: **port 22**, source = **your IP** (not `0.0.0.0`).

**On your Mac** (from the parent directory of the repo):

```bash
scp -r MicrohireAgentChat YOUR_ADMIN@YOUR_VM_IP:C:/Users/YOUR_ADMIN/Desktop/
```

Then RDP in, run **§3 toolchain**, **`az login`**, and **step 9** above from `Desktop\MicrohireAgentChat`.

#### Option C — Git clone on the VM (no file copy)

If the VM can reach GitHub and you have a **PAT** or SSH key:

```powershell
cd C:\work
git clone https://github.com/ORG/MicrohireAgentChat.git
cd MicrohireAgentChat
git checkout master
```

Then toolchain, `az login`, and **`Build-AndDeploy.ps1`** as in step 9.

## 2. Provision VM + NSG + managed identity (Azure CLI)

From your workstation (bash, WSL, or macOS) with `az login`:

```bash
export MY_IP=$(curl -sSf https://ifconfig.me)
export VM_ADMIN_PASSWORD='use-a-long-random-password'
chmod +x Scripts/azure-build-vm/provision-build-vm.example.sh
./Scripts/azure-build-vm/provision-build-vm.example.sh
```

Review and edit variables at the top of [provision-build-vm.example.sh](./provision-build-vm.example.sh) (region, VM name, SKU). The script:

- Creates a resource group, VNet, subnet, NSG with **RDP (3389) only from `MY_IP`**
- Creates a **Windows Server 2022** VM with **system-assigned managed identity**
- Assigns **Website Contributor** on the App Service resource group (`rg-JennyJunkeer-9509` by default)

If `az webapp deploy` fails with authorization errors, assign **Contributor** on that resource group instead (same scope as in the script’s `az role assignment create` command), or scope a role to the specific web app resource.

**Cost:** Stop/deallocate the VM when not building: `az vm deallocate -g <rg> -n <vm>`.

## 3. Toolchain on the VM (one-time)

RDP to the VM. In **Administrator** PowerShell:

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned -Force
cd <path-to-repo>\Scripts\azure-build-vm
.\Install-BuildVmToolchain.ps1
```

Open a **new** PowerShell window, then verify:

- `dotnet --version` (8.x)
- `git --version` (if using clone)
- `az version`

## 4. Azure login on the VM

With **managed identity** (after RBAC propagates, often a few minutes):

```powershell
az login --identity
```

Alternatively: interactive `az login` or service principal (less ideal for long-lived secrets).

## 5. Build and deploy

From the repo root on the VM (PowerShell):

```powershell
cd D:\path\to\MicrohireAgentChat
.\Scripts\azure-build-vm\Build-AndDeploy.ps1 -RepoRoot (Get-Location).Path
```

- **Build only** (no deploy): add `-SkipDeploy`, then copy `site.zip` elsewhere.
- **Pull latest** before build: add `-GitPull` (requires `git` + remote).

Defaults: `ResourceGroup = rg-JennyJunkeer-9509`, `WebAppName = microhire` (same as [deploy.sh](../../deploy.sh)).

## 6. Verification checklist (after deploy)

Complete these on the live site; no automation in-repo.

1. Open the App Service URL (e.g. `https://microhire.azurewebsites.net`) and smoke-test chat.
2. Generate a **quote PDF** path that uses Playwright; confirm it completes (no infinite hang).
3. Confirm **Lead email / linked app** still works: production config points `ChatBaseUrl` / `LeadEmail__ChatBaseUrl` at the other `*.azurewebsites.net` app — unchanged by this deploy unless you altered appsettings in the package.

## Files

| File | Purpose |
|------|---------|
| [Build-AndDeploy.ps1](./Build-AndDeploy.ps1) | Publish win-x64, Playwright Chromium, zip, optional `az webapp deploy` |
| [Install-BuildVmToolchain.ps1](./Install-BuildVmToolchain.ps1) | winget installs: .NET 8 SDK, Git, Azure CLI |
| [provision-build-vm.example.sh](./provision-build-vm.example.sh) | Example `az` commands for VM + NSG + MI role |
