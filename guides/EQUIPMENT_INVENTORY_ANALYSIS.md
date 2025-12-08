# Equipment Inventory Analysis
## How Equipment is Grouped, Categorized, and Packaged

Based on the comprehensive analysis of the production database, here's how equipment is structured in the Microhire system.

## 📊 Database Overview

- **Total Equipment Items:** 4,795 products
- **Packages vs Individual Items:** 618 packages, 4,177 individual items
- **Categories:** 250+ distinct categories
- **Pricing:** Wide range from free to $31,903.90

---

## 📂 Equipment Grouping Structure

### Primary Grouping: `groupFld` Field
Equipment is organized into high-level groups that represent functional areas:

| Group | Items | Description |
|-------|-------|-------------|
| **VENUE** | 1,145 | Venue-specific equipment (screens, staging, furniture) |
| **VISION** | 728 | Video and projection equipment |
| **AUDIO** | 626 | Sound systems, microphones, speakers |
| **CABLE** | 420 | All types of cables and connectors |
| **COMPUTER** | 408 | Laptops, desktops, servers |
| **LIGHTING** | 302 | Lighting fixtures and controllers |
| **RIGGING** | 200 | Truss, motors, lifting equipment |
| **STAGING** | 167 | Stages, platforms, flooring |
| **LED-WALL** | 117 | LED panels and video walls |
| **TOOLS** | 98 | Tools and maintenance equipment |

### Secondary Grouping: Categories
Within each group, items are further categorized:

#### Top Categories by Item Count:
- **VCOMMON** (236 items) - Common video equipment
- **SPEAKER** (129 items) - Speaker systems
- **DISTRIB** (112 items) - Distribution equipment
- **VIDEO** (102 items) - Video equipment
- **PROJECTR** (101 items) - Projectors
- **LCDBRKT** (101 items) - LCD brackets
- **AUDIO** (100 items) - Audio equipment
- **W/MIC** (98 items) - Wireless microphones
- **CAMERAS** (97 items) - Camera equipment
- **POWER** (90 items) - Power equipment

### Tertiary Grouping: Subcategories
Categories are further subdivided:

#### Top Category-Subcategory Combinations:
- **VCOMMON → VVISION** (115 items) - Vision-related video equipment
- **VCOMMON → VLIGHT** (46 items) - Lighting-related video equipment
- **LCDBRKT → MONBRKT** (35 items) - Monitor brackets
- **PROJECTR → LENS** (33 items) - Projector lenses
- **VCOMMON → VAUDIO** (30 items) - Audio-related video equipment

---

## 📦 Package Structure

### Package vs Individual Items
- **Packages:** 618 items (13% of total)
- **Individual Items:** 4,177 items (87% of total)

### Package Examples:
| Package Code | Package Name | Components | Price |
|--------------|--------------|------------|-------|
| WBMARKER | Whiteboard Marker (Pack) | Multiple markers | $15 |
| AHBHBSLX | Single Ballroom Stage Wash Package | Lighting setup | $0 |
| AHBHBRVX | Single Ballroom Projection Package | Projection setup | $0 |
| AHBHBRLX | Single Ballroom Lighting Package | Lighting setup | $0 |
| AHBHBRAX | Single Lawson Ballroom Audio Package | Audio setup | $0 |

### Package Components Analysis
Packages contain multiple component items:

#### Complex Packages (30+ components):
- **VIC2.6P2**: LED Panel 500mm x 500mm P2.6mm LED (30 components)
- **RICOPSR1**: Operator Surround packages (24 components each)
- **PCLPRO**: Dell G15 Production Level Pro (24 components)
- **NPRODPC**: Watchout 6 (22 components)
- **PANA12K**: Panasonic 12K projector packages (21 components)

### Component Relationships
The `vwProdsComponents` view shows how packages are built:
- Parent packages contain child components
- Components have quantities and descriptions
- Complex hierarchies exist (packages within packages)

---

## 💰 Pricing Structure

### Price Distribution:
| Price Range | Items | Avg Price | Min Price | Max Price |
|-------------|-------|-----------|-----------|-----------|
| **FREE** | 4,595 | $0 | $0 | $0 |
| **$0-50** | 41 | $24 | $0.30 | $50 |
| **$50-200** | 24 | $129 | $50 | $199 |
| **$200-500** | 19 | $340 | $200 | $499 |
| **$500-1000** | 30 | $683 | $525 | $995 |
| **$1000-2500** | 46 | $1,596 | $1,050 | $2,454 |
| **$2500+** | 40 | $6,554 | $2,580 | $31,904 |

### Most Expensive Equipment:
| Code | Name | Price | Category |
|------|------|-------|----------|
| **AD-ERACK** | AD E-RACK | $31,904 | ARRAY |
| **FXDC5580** | Fuji Xerox DocuCentre-IV C5580 | $20,000 | PHOTOCPY |
| **CX-Q4K8** | 8-Channel Q-SYS Amplifier | $10,908 | QSC/QSYS |
| **V2400.8** | Voltera D 2400.8 Amplifier | $9,846 | BIAMP |
| **CORE110F** | DSP Core 128x128 | $7,954 | QSC/QSYS |

---

## 🔧 Equipment Categories Deep Dive

### Audio Equipment (AUDIO, SPEAKER, MICROPH, etc.)
- Wireless microphones (W/MIC: 98 items)
- Speaker systems (SPEAKER: 129 items)
- Mixers and amplifiers
- Audio processing equipment

### Video Equipment (VIDEO, PROJECTR, CAMERAS, etc.)
- Projectors (PROJECTR: 101 items)
- Cameras (CAMERAS: 97 items)
- Video distribution (DISTRIB: 112 items)
- LED walls and displays

### Lighting Equipment (LIGHTING, LXMHEAD, LED, etc.)
- Moving heads (LXMHEAD: 44 items)
- LED fixtures (LED: 45 items)
- Lighting controllers and dimmers
- Smoke effects (SMOKEFX: 44 items)

### Staging & Rigging (STAGING, TRUSS, SCAFFOLD, etc.)
- Truss systems (TRUSS: 83 items)
- Staging platforms
- Scaffolding (SCAFFOLD: 24 items)
- Rigging hardware

### Computer Equipment (COMPUTER, LAPTOP, DESKTOP, etc.)
- Production laptops
- Desktops and servers
- Tablets and peripherals

---

## 🏗️ Package Creation Logic

### How Packages are Built:
1. **Parent Package**: Main package item (e.g., "Sound System Package")
2. **Component Items**: Individual pieces that make up the package
3. **Quantities**: How many of each component are included
4. **Hierarchy**: Packages can contain other packages

### Package vs Component Relationship:
```
Sound System Package (Parent)
├── Wireless Microphone × 2
├── Speaker × 4
├── Mixer × 1
├── Cables × 10
└── Microphone Stands × 2
```

### Booking Integration:
When a package is added to a booking:
- The package appears as one line item in `tblitemtran`
- Components are tracked separately for inventory
- Pricing can be package-based or component-based

---

## 📋 Key Insights for Booking Creation

### 1. **Package Selection**:
- Use packages for common setups (e.g., "Ballroom Lighting Package")
- Packages reduce booking complexity
- Components are automatically included

### 2. **Individual Item Selection**:
- For custom requirements
- When specific items are needed
- For specialized equipment

### 3. **Pricing Strategy**:
- Most equipment is free or low-cost ($0-200 range)
- High-end equipment ($2500+) for premium bookings
- Packages often priced as complete solutions

### 4. **Inventory Management**:
- Track both packages and individual components
- Component availability affects package availability
- Categories help with equipment organization

### 5. **Grouping for Operations**:
- Use `groupFld` for operational planning
- Categories for equipment maintenance
- Subcategories for detailed specifications

---

## 🎯 Recommendations for Booking System

1. **Package-First Approach**: Present packages as primary options
2. **Component Visibility**: Allow drilling down into package components
3. **Category Navigation**: Use category/subcategory for equipment browsing
4. **Pricing Transparency**: Show both package and component pricing
5. **Inventory Integration**: Check component availability for packages

This structure provides a comprehensive framework for understanding and working with the equipment inventory system.
