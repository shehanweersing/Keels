# Live Source Discovery & Feasibility Audit — PriceWise LK

Date of Audit: 2026-11-22  
Auditor: PriceWise LK Engineering Team  
Methodology: Direct live HTTP inspection, header analysis, robots.txt review, payload inspection, and client script analysis.

---

## Executive Summary

| Supermarket | Online Platform | Status | Permitted Access Method | Data Format | Key Findings |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Cargills Food City** | `cargillsonline.com/Web/` | **Verified & Permitted** | Public ASP.NET POST search endpoint (`/Web/GetSearchBarProductsV1`) | Structured JSON | Accessible without CAPTCHA or blocking. Real item names, unit sizes, units of measure, SKU codes, images, and prices in LKR. |
| **Arpico** | `myarpico.com/shop/` | **Verified & Permitted** | Public OpenCart search & catalog routes (`index.php?route=product/search`) | HTML (OpenCart) | Robots.txt explicitly allows search indexing. Semantic HTML with current price, old price (discounts), titles, images, and product URLs. |
| **Keells** | `keellssuper.com` | **Blocked (Auth & Session Encryption Required)** | None verified as publicly permitted | React SPA calling `zebraliveback.keellssuper.com` | Backend APIs require encrypted `usersessionid` and Cloudflare challenge cookies. Direct unauthenticated requests return `HTTP 401 Unauthorized`. Automated bypass is strictly prohibited by project ethical access rules. |

---

## 1. Cargills Food City

### Platform Details
- **Website**: `https://cargillsonline.com/Web/`
- **Architecture**: ASP.NET MVC 5.2 on Windows Server behind Cloudflare.
- **Session/Cookies**: Automatically initializes cookies on landing:
  - `ASP.NET_Pincode=Colombo`
  - `ASP.NET_WebStoreType=1`
  - `Asp.Net_WebStoreId=1031` (Colombo Central fulfillment store)

### Investigation & Permitted Access Method
- **Robots.txt**: `https://cargillsonline.com/robots.txt` returned HTTP 404 (Not Found).
- **Public Search Endpoint**: `POST https://cargillsonline.com/Web/GetSearchBarProductsV1`
  - Request headers:
    - `Content-Type: application/x-www-form-urlencoded; charset=UTF-8`
    - `X-Requested-With: XMLHttpRequest`
    - `Cookie: ASP.NET_Pincode=Colombo; ASP.NET_WebStoreType=1; Asp.Net_WebStoreId=1031`
  - Request body: `Search={query}` (e.g. `Search=milk`, `Search=tea`, `Search=rice`)
  - Response: Direct JSON array of verified supermarket items.

### Sample Live Payload (Retrieved 2026-11-22)
```json
[
  {
    "Id": 154,
    "SKUCODE": "BV20102",
    "ItemName": "Milkmaid Condensed Milk",
    "ItemImage": "/VendorItems/MenuItems/BV20102_2.jpg",
    "CategoryId": "6",
    "MenuCategoryName": "Dairy",
    "Uom": "g",
    "UnitSize": "510.00",
    "Inventory": "990.00",
    "EnId": "Em1GPI1wiW+z9w89sKr0+g=="
  },
  {
    "Id": 1523,
    "SKUCODE": "BV05004",
    "ItemName": "Milca Instant Milk Powder",
    "ItemImage": "/VendorItems/MenuItems/BV05004_2.jpg",
    "CategoryId": "6",
    "MenuCategoryName": "Dairy",
    "Uom": "g",
    "UnitSize": "400.00",
    "Inventory": "1110.00",
    "EnId": "m+ISAS..."
  }
]
```

### Field Mapping
- **Product Name**: `ItemName`
- **Quantity & Unit**: `UnitSize` (e.g., `510.00`) and `Uom` (e.g., `g`, `ml`, `kg`)
- **Price (LKR)**: `Inventory` (Mapped to price in frontend script: `Rs." + objdetails.Inventory`)
- **Source SKU / Barcode**: `SKUCODE`
- **Category**: `MenuCategoryName` (Id: `CategoryId`)
- **Image URL**: `https://cargillsonline.com` + `ItemImage`
- **Product Details Link**: `https://cargillsonline.com/Web/ProductDetails?ID=` + `EnId`

### Operational Parameters
- **Recommended Schedule**: Every 15-30 minutes for top search terms and categories.
- **Rate Limit**: Max 1 request per 2 seconds, with exponential backoff on HTTP 429/5xx.
- **Availability Verification**: Active items returned in live search with valid prices.

---

## 2. Arpico Supercentre

### Platform Details
- **Website**: `https://myarpico.com/shop/` (Redirects to OpenCart base `https://myarpico.com/index.php`)
- **Architecture**: OpenCart e-commerce engine behind Cloudflare.
- **Currency**: LKR (`currency=LKR`).

### Investigation & Permitted Access Method
- **Robots.txt**: Explicitly grants search indexers permission via Content Signals:
  - `Content-Signal: yes` for search indexing.
  - Public routes permitted: `index.php?route=product/search`, `index.php?route=product/category`, `index.php?route=product/product`.
- **Public Search Route**: `GET https://myarpico.com/index.php?route=product/search&language=en-gb&search={query}`
  - No session or authentication required. Standard browser User-Agent header accepted.
  - Returns paginated HTML containing product thumbs (`div.product-thumb`).

### Sample Live Extraction (Retrieved 2026-11-22)
- Product: **Ambewela Fresh Milk 200Ml**
  - Source Product ID: `5344`
  - URL: `https://myarpico.com/index.php?route=product/product&language=en-gb&product_id=5344&search=milk`
  - Image: `https://myarpico.com/image/cache/catalog/products/97949-228x228.png`
  - Price: `Rs. 140.00`
- Product: **Iodex Balm 18G**
  - Source Product ID: `308`
  - Original Price: `Rs. 375.00`
  - Discounted Current Price: `Rs. 318.80`

### Field Mapping
- **Product Name**: Element `h4.product-title a` or `div.caption h4 a`
- **Product URL**: `a[href]` inside product thumb
- **Image URL**: `div.image img[src]`
- **Current Price (LKR)**: `.price-new` (if discounted) or raw text inside `p.price`
- **Original Price (LKR)**: `.price-old` (when discount is active)
- **Availability**: Present in search listing with add-to-cart button enabled.

### Operational Parameters
- **Recommended Schedule**: Every 30-60 minutes for key categories.
- **Rate Limit**: Max 1 request per 3 seconds. Respect server responsiveness.

---

## 3. Keells Super

### Platform Details
- **Website**: `https://www.keellssuper.com/`
- **Architecture**: Single Page Application (React/Webpack) backed by Azure App Services (`zebraliveback.keellssuper.com`, `jmsl-keellsonline-datasync.azurewebsites.net`) protected by Cloudflare.
- **Robots.txt**: Disallows `/cart`, `/checkout/`, `/myAccount`, `/PaymentGateway`. Product pages are not explicitly disallowed in robots.txt.

### Investigation & Access Assessment
- **Client Script Analysis**: The React bundle (`main.a71a2d97.chunk.js`) routes API requests to `https://zebraliveback.keellssuper.com`.
- **Authentication & Encryption**:
  - Every API request requires a custom HTTP header: `usersessionid: <encrypted_token>`.
  - The token is encrypted on the client side using a proprietary cryptographic routine (`Object(m.a)(userSessionID)`).
  - Direct HTTP calls to `https://zebraliveback.keellssuper.com/api/...` or `https://zebraliveback.keellssuper.com/Web/...` without this encrypted session return `HTTP 401 Unauthorized`.
- **Ethical & Legal Compliance**:
  - The project rules explicitly forbid bypassing access controls, cracking client-side encryption tokens, or utilizing undocumented private APIs without authorization.
  - Capturing and replaying private user session cookies or attempting to reverse-engineer session encryption routines violates the safety and ethical guidelines.

### Integration Verdict
- **Status**: **Blocked (Authentication & Session Encryption Required)**
- **Required Remediation**: Direct authorized API partnership or dedicated B2B data feed from Jaykay Marketing Services (Pvt) Ltd (Keells Super).
- **Application Behavior**: The PriceWise LK application will accurately report Keells with status `Blocked / Awaiting Authorized Feed`. No fake data will be provided.

---

## Source Feasibility Summary

| Source | Integration Status | Collector Strategy |
| :--- | :--- | :--- |
| **Cargills Food City** | **Active (Primary)** | Direct HttpClient calling public JSON search API |
| **Arpico** | **Active (Secondary)** | Direct HttpClient parsing public OpenCart HTML catalog |
| **Keells** | **Blocked** | Architectural placeholder reporting actual blocked status |
