# Media & Image Storage — Plan

## Context
The storefront needs admin-friendly **image uploads**. Today there are none: product
images are **URL strings** an admin pastes in (`ProductImages.Url`), the Excel import
carries one `ImageUrl` column per row, and the `MediaFiles` table (migration 011) is
unused. The API serves no static files (Nginx serves the Angular build + proxies `/api`).
Current image data is external `picsum.photos` placeholders.

We want a real **Upload** button in admin, images that **survive redeploys**, and a design
that keeps the database lean and can graduate to a CDN later.

**Decision:** product images → **local disk + Nginx**, behind an `IMediaStorage` seam so
switching to cloud object storage (Cloudflare R2 / S3) later is one class + config, no
controller/UI churn. Banners stay on the DB-blob approach already built (few, small).

---

## Current state (facts)
- **Product images:** `ProductImages(Url, AltText, DisplayOrder, IsPrimary, MediaFileId[unused])`.
  Admin form = paste "Image URL" text box (no file picker). `ProductService.BuildImages`
  just stores whatever `Url` string it's given.
- **Excel:** `ProductImportService` has an `ImageUrl` column → sets the primary image URL.
- **MediaFiles table:** exists (FileName, MimeType, SizeBytes, Url, Width, Height, folder) — **unused**.
- **Static serving:** none in the API. Nginx serves `/var/www/ecomm/web`, proxies `/api`.
- **Banners (in progress):** bytes in MySQL `LONGBLOB`, streamed via `GET /api/cms/banners/{id}/image`.
  Backend built; frontend + deploy pending. Fine as-is for a handful of banners.

---

## Target design (product images)

### 1. Storage abstraction — `IMediaStorage`
```
Task<StoredFile> SaveAsync(Stream data, string contentType, string originalName, CancellationToken ct);
// StoredFile { string Url; long Size; int? Width; int? Height; }
```
- **`LocalDiskStorage`** (build now): writes to `Media:UploadPath`
  (prod `/var/www/ecomm/uploads`, dev `./uploads`), path `/{yyyy}/{MM}/{guid}.{ext}`,
  returns public URL `/uploads/{yyyy}/{MM}/{guid}.{ext}`.
- Later: `R2Storage` / `S3Storage` — same interface, returns a CDN URL. No other code changes.

### 2. Upload endpoint — `POST /api/admin/media` (Admin only, multipart)
- Reuse the banner upload guardrails: max 5 MB, allow `image/jpeg|png|webp|gif`.
- Flow: validate → `IMediaStorage.SaveAsync` → insert a **`MediaFiles`** row → return
  `{ mediaFileId, url, width, height }`.
- New feature slice: `Features/Media/` (`MediaController`, `MediaService`, `IMediaStorage` + `LocalDiskStorage`).
- Config: `Media:UploadPath`, `Media:PublicBaseUrl` (default `/uploads`), `Media:MaxBytes`.

### 3. Wire the admin product form
- `admin-product-form.component.html`: each image row gets an **"Upload"** button next to the
  existing URL box. Upload → `POST /api/admin/media` → set `img.url` = returned url,
  `img.mediaFileId`. Paste-URL still works (external/CDN images).
- Backend: `ProductImageInput` gains optional `mediaFileId`; `ProductService.BuildImages`
  persists it (links `ProductImages.MediaFileId` → `MediaFiles`). Minimal change.

### 4. Infra (one-time, document in deployment.md)
- `mkdir -p /var/www/ecomm/uploads && chown -R www-data:www-data /var/www/ecomm/uploads`.
- Nginx: `location /uploads/ { alias /var/www/ecomm/uploads/; expires 30d; access_log off; }`.
- Deploy script + web-dir wipe (`rm -rf /var/www/ecomm/web/*`) must **never** touch `uploads`.
- Add `/var/www/ecomm/uploads` to backups.
- API `appsettings`/env: `Media__UploadPath=/var/www/ecomm/uploads`.

### 5. Excel import — unchanged
- URL column stays (correct for bulk — reference already-hosted URLs).
- *(Optional later)* "match a ZIP of images to SKUs by filename" bulk uploader.

---

## Files touched
- **New:** `ecomm.api/Features/Media/MediaController.cs`, `MediaService.cs`, `IMediaStorage.cs`, `LocalDiskStorage.cs`.
- **New DTO/entity:** `MediaFile` entity + DbContext mapping (`ToTable("MediaFiles")`); DbSet.
- **Edit:** `Program.cs` (DI + config bind), `ProductService.cs` (persist `MediaFileId`),
  `CatalogDtos.cs` (`ProductImageInput.MediaFileId`), `admin-product-form.component.{ts,html}`,
  a small `MediaService` (frontend) for the upload call.
- **Edit:** `documents/deployment.md` (uploads dir + Nginx + backup).
- **No migration needed** for `MediaFiles` (table already exists); a tiny one only if we add columns.

## Verification
- Dev: set `Media:UploadPath=./uploads`, `dotnet run`, `ng serve`. Upload an image in the product
  form → file lands in `./uploads/yyyy/MM/…`, `MediaFiles` row created, image renders on the
  product page. Paste-URL path still works. Excel import/export unchanged.
- Prod: after Nginx `location /uploads/`, upload → image loads from `https://calendarshop.online/uploads/…`;
  redeploy (wipe web dir) → image still loads (dir untouched).

## Later (graduation to CDN — Option D)
Add `R2Storage : IMediaStorage` (Cloudflare R2, S3-compatible, generous free tier), keys in
user-secrets/env, flip `Media:Provider=R2`. Existing `/uploads/*` URLs keep working; new
uploads return CDN URLs. No controller/UI/DB changes.

---

## Separately: finish the banner feature (already started, independent)
Backend done (migration 023 `HomeBanners`, entity, `BannerService`, public+admin controllers, DI).
Remaining: home reads banners from API (resolver + fallback), admin Banners page (upload/edit/
reorder/delete), build, deploy (frontend + migration 023, **skip re-running 017**).
