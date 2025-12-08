#!/usr/bin/env python3
"""
Script to upload database schema files to GitHub Gist
Requires: pip install requests
"""

import json
import os
import requests
from pathlib import Path

# You'll need to set this with your GitHub personal access token
# Create one at: https://github.com/settings/tokens
GITHUB_TOKEN = os.getenv('GITHUB_TOKEN', '')  # Set this environment variable

def create_gist(description, files, public=True):
    """Create a GitHub Gist with multiple files"""

    if not GITHUB_TOKEN:
        print("❌ Error: GITHUB_TOKEN environment variable not set!")
        print("Create a token at: https://github.com/settings/tokens")
        print("Then run: export GITHUB_TOKEN=your_token_here")
        return None

    url = "https://api.github.com/gists"
    headers = {
        "Authorization": f"token {GITHUB_TOKEN}",
        "Accept": "application/vnd.github.v3+json"
    }

    data = {
        "description": description,
        "public": public,
        "files": files
    }

    response = requests.post(url, headers=headers, json=data)

    if response.status_code == 201:
        gist_data = response.json()
        print(f"✅ Gist created successfully!")
        print(f"📋 URL: {gist_data['html_url']}")
        return gist_data['html_url']
    else:
        print(f"❌ Error creating gist: {response.status_code}")
        print(f"Response: {response.text}")
        return None

def load_schema_files():
    """Load all JSON files from database_schema directory"""
    schema_dir = Path("database_schema")
    files = {}

    if not schema_dir.exists():
        print("❌ database_schema directory not found!")
        return None

    # Load each JSON file
    for json_file in schema_dir.glob("*.json"):
        try:
            with open(json_file, 'r') as f:
                content = f.read()
                files[json_file.name] = {"content": content}
                print(f"📄 Loaded: {json_file.name} ({len(content)} chars)")
        except Exception as e:
            print(f"❌ Error loading {json_file.name}: {e}")
            continue

    return files

def main():
    print("🚀 GitHub Gist Upload Script")
    print("=" * 50)

    # Load files
    files = load_schema_files()
    if not files:
        return

    # Create gist description
    description = "Microhire Database Schema Documentation - AITESTDB Complete Table Structures"

    print(f"\n📦 Ready to upload {len(files)} files")
    print(f"📝 Description: {description}")

    # Confirm before uploading
    confirm = input("\nDo you want to create the Gist? (y/N): ").lower().strip()
    if confirm not in ['y', 'yes']:
        print("❌ Upload cancelled.")
        return

    # Create the gist
    gist_url = create_gist(description, files, public=True)

    if gist_url:
        print(f"\n🎉 Success! Share this link with your team:")
        print(f"🔗 {gist_url}")
        print("\n💡 Your team can now:")
        print("   - View all table schemas in one place")
        print("   - Search through the JSON data")
        print("   - Download individual files")
        print("   - Fork and modify if needed")

if __name__ == "__main__":
    main()
