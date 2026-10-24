# چطور فایل TamsDesk.exe را بسازیم

برنامه گرافیکی است (WPF) ولی باید روی **ویندوز** کامپایل شود. دو راه دارید.

---

## راه ۱ — بدون نصب هیچ ابزاری، با GitHub (ساده‌ترین)

EXE روی سرور خود GitHub ساخته می‌شود و شما فقط دانلودش می‌کنید.

۱. یک مخزن (repository) خالی روی GitHub بسازید.
۲. محتوای این پوشه را در آن push کنید:
   ```bash
   git init
   git add .
   git commit -m "TAMSDESK"
   git branch -M main
   git remote add origin https://github.com/USERNAME/REPO.git
   git push -u origin main
   ```
۳. در GitHub به تب **Actions** بروید؛ یک اجرا به نام «Build TAMSDESK exe» خودکار شروع می‌شود (۲ تا ۳ دقیقه).
۴. وقتی سبز شد، روی آن اجرا کلیک کنید و از پایین صفحه، بخش **Artifacts**، فایل **TamsDesk-exe** را دانلود کنید. داخلش `TamsDesk.exe` است.

> قبل از push، اگر سرور رله‌تان آماده است، آدرسش را در `TamsDesk.Client/Config.cs` بگذارید.
> فایل workflow از قبل در `.github/workflows/build.yml` هست؛ کاری با آن ندارید.

---

## راه ۲ — ساخت روی ویندوز خودتان

۱. [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0) را نصب کنید.
۲. آدرس رله را در `TamsDesk.Client/Config.cs` تنظیم کنید.
۳. در PowerShell داخل پوشه‌ی `TamsDesk.Client`:
   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained true `
     -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
   ```
۴. فایل خروجی:
   ```
   TamsDesk.Client\bin\Release\net8.0-windows\win-x64\publish\TamsDesk.exe
   ```

---

## نکته درباره‌ی هشدار ویندوز (SmartScreen)

چون EXE امضای دیجیتال (code-signing) ندارد، بار اول ویندوز ممکن است هشدار
«Windows protected your PC» بدهد. روی **More info ← Run anyway** بزنید.
برای حذف کامل این هشدار باید یک گواهی Code Signing بخرید و EXE را امضا کنید.
