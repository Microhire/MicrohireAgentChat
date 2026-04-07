#!/bin/bash
# Point local appsettings.json BookingsDb back at the LOCAL VM (192.168.2.151, shared with RentalPoint)
set -e
FILE="MicrohireAgentChat/appsettings.json"

# Comment out PROD, uncomment LOCAL
perl -i -pe 's|^(\s*)"BookingsDb": "Server=116\.90\.5\.144.*|\1// "BookingsDb": "Server=116.90.5.144\\\\SQLEXPRESS,41383;Database=AITESTDB;User Id=PowerBI-Consult;Password=2tW\@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=True;",|' "$FILE"
perl -i -pe 's|^(\s*)// "BookingsDb": "Server=192\.168\.2\.151.*|\1"BookingsDb": "Server=192.168.2.151,1433;Database=AITESTDB;User Id=sa;Password=Intent\@2024!Secure;TrustServerCertificate=True;",|' "$FILE"

echo "✅ BookingsDb -> LOCAL VM (192.168.2.151 AITESTDB)"
grep -n '"BookingsDb"' "$FILE" | grep -v '^.*://'
