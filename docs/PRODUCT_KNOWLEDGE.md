# Product Knowledge Guide

## Purpose

This guide is the canonical product knowledge for Microhire: categories of AV equipment, equipment types, event-centric recommendations, integration and scalability, operational notes and support, availability by season, and **warehouse stock** (which warehouse holds which product and counts where applicable).

It reflects products available in Microhire's warehouses and registered in its software, RentalPoint.

Two scopes are maintained:

- **Master + Subwarehouses**: Brisbane (Main), Sydney (WH2), Melbourne (WH3).
- **Westin Brisbane on-site**: Dedicated AV warehouse inside The Westin Brisbane.

---

## Where product information lives

### RentalPoint / Database

- **tblInvmas**: Product codes, categories, descriptions, `on_hand` (quantity). Used for search, recommendations, and quoting. There is no per-warehouse column in the app's schema; warehouse-specific counts are maintained in the narrative product knowledge (see below).
- **tblRatetbl**: Pricing.
- **VwProdsComponents**: Package components.

These are used by `SmartEquipmentRecommendationService`, `EquipmentSearchService`, and quote/booking flows.

### Room / venue packages

- **venue-room-packages.json** (`MicrohireAgentChat/wwwroot/data/venue-room-packages.json`): Maps Westin Brisbane and Four Points Brisbane rooms to WSB audio, vision, and AV package product codes. Used by `SmartEquipmentRecommendationService` for room-specific recommendations.

### Bundles and drivers

- **item-rules.json** (`MicrohireAgentChat/wwwroot/data/item-rules.json`): Drives bundle composition and quantity rules (e.g. wireless mic bundles, laptop counts). Used by recommendation and quote logic.

### Narrative product knowledge

- **product-knowledge-master.json** (`MicrohireAgentChat/wwwroot/data/product-knowledge-master.json`): Categories, equipment types, event recommendations, integration, operational notes, availability, and **warehouse stock** for Brisbane (Main), Sydney (WH2), and Melbourne (WH3).
- **product-knowledge-westin.json** (`MicrohireAgentChat/wwwroot/data/product-knowledge-westin.json`): Same structure for the Westin Brisbane on-site warehouse only, including inventory counts and optional "External support" notes per category.

These files are **not** used for pricing or bookable package resolution; they are used for **explanation and discovery** (e.g. "what do you have at Westin?", "what do you recommend for a gala?", "which warehouse has X?").

---

## How the AI uses it

### Instructions

Isla's agent instructions include a short **Product & warehouse overview** that states:

- Products are recorded in RentalPoint; inventory is held at Master warehouses (Brisbane, Sydney, Melbourne) and at Westin Brisbane on-site.
- The eight categories by name.
- When to use Westin on-site vs central warehouses.
- To call **get_product_knowledge** for detailed product descriptions, event recommendations, operational notes, availability by season, or which warehouse holds which product.

### Tool: get_product_knowledge

- **Name**: `get_product_knowledge`
- **Parameters**: Optional `category` (e.g. "Audio", "Lighting"), optional `warehouse_scope` ("master" | "westin"; omit for both).
- **Behaviour**:
  - No args: Returns a short overview (all categories, which scope covers which warehouses).
  - `category` only: Returns that category's full section (description, equipment types, event recommendations, integration, operational notes, availability, warehouse stock) for both master and Westin when applicable.
  - `warehouse_scope` only: Returns all categories for that scope with warehouse stock emphasised.
  - Both: Returns that category for that warehouse scope only.

The handler loads the appropriate JSON file(s), filters by category and/or scope, formats a readable summary (markdown), and returns it as `outputToUser`. The AI is instructed to output `outputToUser` exactly so warehouse names, counts, and event recommendations are shown verbatim.

### Other tools

- **recommend_equipment_for_event** uses the database, item-rules, and venue-room-packages only; it does **not** read the product-knowledge JSON. The knowledge guide is for explanation and discovery; actual recommendations and pricing come from existing services.

---

## Layout per product category

For each category, the JSON (and thus the tool output) includes:

| Section | Description |
|--------|-------------|
| **Description** | What the category is and how it supports events. |
| **Equipment types** | List of equipment types (e.g. PA systems, speakers, microphones). |
| **Event recommendations** | What products are needed for which event type (boardroom, conference, gala, workshop, etc.). |
| **Integration & scalability** | How the category integrates with other AV and scales. |
| **Operational notes & support** | Calibration, backup kit, safety, lead times. |
| **Availability & seasons** | Peak / normal / lean or seasonal notes. |
| **Warehouse stock** | Which warehouse holds what; counts per item where applicable. |

Westin categories may also include **External support** (e.g. in-ear monitoring and line arrays from Brisbane HQ).

---

## How to update

1. **Categories, event recommendations, operational notes, availability, warehouse stock**: Edit `product-knowledge-master.json` and/or `product-knowledge-westin.json`. The app reads these at runtime when `get_product_knowledge` is called; no code change is required. Restart the app if you need changes to be picked up immediately (files are read on each tool call; no in-memory cache is currently applied).
2. **Pricing and bookable packages**: Continue to use the database, `venue-room-packages.json`, and `item-rules.json`. Keep narrative stock counts in the product-knowledge JSON in sync with operations when warehouse stock changes.
3. **Room-specific WSB packages**: Update `venue-room-packages.json` and/or the database; see existing room-aware logic in `SmartEquipmentRecommendationService`.
