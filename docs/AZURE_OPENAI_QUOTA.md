# Azure OpenAI rate limits and quota increase

When you see errors like:

```text
Your requests to MH-gpt-4o for gpt-4o in Australia East have exceeded the token rate limit
for your current AIServices S0 pricing tier. Please retry after X seconds.
```

the app now **waits the suggested number of seconds** and retries up to 6 times, so many rate-limit cases resolve without changes.

To **increase your quota** (and avoid hitting the limit as often), use one of the options below.

## Option 1: Azure Portal (recommended)

1. Open [Azure Portal](https://portal.azure.com) and sign in.
2. Go to **All resources** and open your **Azure OpenAI** resource (e.g. `microhire-test-project-resource`).
3. In the left menu, open **Resource management** → **Quotas** (or **Manage** → **Quotas**).
4. Find the row for **gpt-4o** (or your deployment model) in **Australia East**.
5. Click **Request quota increase** (or **Increase quota**), enter the new **Tokens per minute (TPM)** you want, add a short justification, and submit.

Alternatively use the direct link (replace subscription/resource if needed):

- [Azure OpenAI – Request quota increase](https://aka.ms/oai/quotaincrease)

## Option 2: Check usage with Azure CLI

You’re already logged in with `az login`. To see current usage and limits for your region:

```bash
# List Cognitive Services (OpenAI) usage for Australia East
az cognitiveservices usage list --location australiaeast -o table
```

To find your OpenAI resource and open its quota blade:

```bash
# List Azure OpenAI resources in the current subscription
az cognitiveservices account list --query "[?kind=='OpenAI'].{name:name, resourceGroup:resourceGroupName, location:location}" -o table
```

Then in the portal: **All resources** → select that resource → **Quotas**.

## Option 3: Run the helper script

From the repo root:

```bash
./scripts/azure-openai-quota.sh
```

This prints your current usage and opens the quota-increase page in your browser (macOS).

## Pricing note

Higher TPM quota is billed only for what you use (same per-token price). Requesting a higher limit does not by itself increase cost; it only allows more tokens per minute when traffic spikes.
