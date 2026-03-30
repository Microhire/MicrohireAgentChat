# Technical Specification: Equipment Database Structure (AITESTDB)

## Overview
The equipment and package structure in the database (AITESTDB) is organized to support the AI-driven booking process. This involves a hierarchy starting from the venue level down to individual packages and specific equipment items.

### Database Details
- **Database:** `AITESTDB`
- **Primary Table:** `tblinvmas`
- **Key Columns:**
  - `product_code`: The unique alphanumeric identifier.
  - `category`: Set to `WSB` for all venue-specific AI packages.
  - `groupFld`: Set to `VENUE` for organizational grouping.
  - `product_Config`: `1` for packages, `0` for folders/independent items.

---

## Folder Hierarchy and DB Mapping
In the Rental Point interface, equipment is organized into folders. It is critical to distinguish between folder codes (visual containers) and product codes (stored as records in `tblinvmas`).

### 1. Folder Nodes (UI Only)
The following codes represent folder/group containers and do **not** have corresponding records in `tblinvmas`:
- `WSB` (Westin Brisbane Packages)
- `WSBELEV` (AI Elevate)
- `WSBBALL` (AI Westin Ballroom)
- `WSBTHRV` (AI Thrive)
- `WSBAX`, `WSBVX`, `WSBAV` (General Department Folders)

### 2. Product and Package Records (Database Level)
These are the actual objects that the application fetches based on the selected venue and room.

#### Room: Westin Elevate
*AI Folder Code: `WSBELEV` (Internal Use Only)*

| Product Code | Description |
| :--- | :--- |
| `ELEVAVP` | Elevate AV Package |
| `ELEVCSS` | Elevate Ceiling Speaker System |
| `ELEVIND` | Independent Items |
| `ELEVPROJ` | Elevate Projection Package |
| `ELEVSAVP` | Elevate (Single) AV Package |
| `ELEVSCSS` | Elevate Single Ceiling Speaker System |

#### Room: Westin Ballroom
*AI Folder Code: `WSBBALL` (Internal Use Only)*

| Product Code | Description |
| :--- | :--- |
| `WBAVP` | Westin Ballroom AV Package |
| `WBDPROJ` | Westin Ballroom Dual Projector Package |
| `WBFBCSS` | Westin Full Ballroom Ceiling Speaker System |
| `WBIND` | Independent Items |
| `WBSAVP` | Westin Ballroom (Single) AV Package |
| `WBSBCSS` | Westin Single Ballroom Ceiling Speaker System |
| `WBSNPROJ` | Westin Ballroom Single Projector (North) Package |
| `WBSPROJ` | Westin Ballroom Single Projector Package |
| `WBSSPROJ` | Westin Ballroom Single Projector (South) Package |

#### Room: Thrive Boardroom
*AI Folder Code: `WSBTHRV` (Internal Use Only)*

| Product Code | Description |
| :--- | :--- |
| `THRVAVP` | Thrive AV Package |
| `THRVCSS` | Thrive Ceiling Speaker System |
| `THRVIND` | Independent Items |
| `THRVPROJ` | Thrive Projection Package |

---

## Technical Implementation Notes
When querying the database for equipment related to a specific room, use the explicit product codes listed above. These are mapped in `wwwroot/data/venue-room-packages.json`.

### Example SQL Retrieval
To fetch all AI packages for the Westin Ballroom:

```sql
SELECT product_code, descriptionV6, category, groupFld
FROM tblinvmas
WHERE product_code IN ('WBAVP', 'WBDPROJ', 'WBFBCSS', 'WBIND', 'WBSAVP', 'WBSBCSS', 'WBSNPROJ', 'WBSPROJ', 'WBSSPROJ')
AND category = 'WSB'
AND groupFld = 'VENUE';
```

---
*End of Specification*
