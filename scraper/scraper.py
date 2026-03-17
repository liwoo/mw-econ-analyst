"""
Bridgepath Capital Financial Reports Scraper
─────────────────────────────────────────────
Process one month at a time:
  1. Discover all report URLs for that month
  2. Download each report (metadata + PDF)
  3. Move to the previous month
  4. Stop after MAX_EMPTY_MONTHS consecutive empty months

Output structure:
  bridgepath-financial-reports/
    MM-YYYY/
      <report-slug>/
        metadata.txt
        <report>.pdf

Run:    python bridgepath_scraper.py
Needs:  pip install requests beautifulsoup4
"""

import os
import re
import time
import requests
from bs4 import BeautifulSoup
from datetime import datetime
from urllib.parse import urlparse

# ── Config ─────────────────────────────────────────────────────────────────────

BASE_URL        = "https://www.bridgepathcapitalmw.com"
OUTPUT_DIR      = "data"
START_YEAR      = 2026
START_MONTH     = 3
MAX_EMPTY_MONTHS = 4    # stop when this many months in a row have nothing
REQUEST_DELAY   = 1.5   # seconds between requests
REQUEST_TIMEOUT = 30
MAX_RETRIES     = 3

HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/120.0.0.0 Safari/537.36"
    ),
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
    "Accept-Language": "en-US,en;q=0.5",
    "Connection": "keep-alive",
}

# ── Helpers ─────────────────────────────────────────────────────────────────────

def get_page(url):
    for attempt in range(1, MAX_RETRIES + 1):
        try:
            r = requests.get(url, headers=HEADERS, timeout=REQUEST_TIMEOUT)
            r.raise_for_status()
            return r
        except requests.exceptions.RequestException as exc:
            print(f"      [retry {attempt}/{MAX_RETRIES}] {exc}")
            if attempt < MAX_RETRIES:
                time.sleep(attempt * 2)
    print(f"      ✗ Giving up on: {url}")
    return None


def prev_month(year, month):
    return (year - 1, 12) if month == 1 else (year, month - 1)


# ── Step A: discover all article URLs for one month ──────────────────────────

def discover_month(year, month):
    """Return list of article URLs found in /YYYY/MM/ (all paginated pages)."""
    urls = []
    page = 1

    while True:
        if page == 1:
            archive_url = f"{BASE_URL}/index.php/{year}/{month:02d}/"
        else:
            archive_url = f"{BASE_URL}/index.php/{year}/{month:02d}/page/{page}/"

        print(f"    Fetching archive page {page}: {archive_url}")
        r = get_page(archive_url)
        if r is None:
            break

        soup = BeautifulSoup(r.content, "html.parser")
        articles = soup.find_all("article", class_="blog-entry")

        if not articles:
            if page == 1:
                print(f"    → No articles found for {year}/{month:02d}")
            break

        for art in articles:
            h2 = art.find("h2", class_="blog-entry-title")
            if h2:
                a = h2.find("a", href=True)
                if a and a["href"] not in urls:
                    urls.append(a["href"])

        # Check for next page
        has_next = soup.find("a", class_="next") or soup.find(
            "a", string=re.compile(r"next|»|›", re.I)
        )
        if has_next:
            page += 1
            time.sleep(REQUEST_DELAY)
        else:
            break

    return urls


# ── Step B: scrape + download one report ─────────────────────────────────────

def parse_url_parts(url):
    """Extract (datetime, slug) from /index.php/YYYY/MM/DD/slug/"""
    parts = [p for p in urlparse(url).path.split("/") if p]
    try:
        dt   = datetime(int(parts[1]), int(parts[2]), int(parts[3]))
        slug = parts[4]
        return dt, slug
    except (IndexError, ValueError):
        return None, (parts[-1] if parts else "unknown")


def find_pdf_url(soup):
    # WordPress file block <object>
    for tag in soup.find_all("object", {"data": re.compile(r"\.pdf", re.I)}):
        return tag["data"]
    # WordPress file block <a>
    for div in soup.find_all("div", class_=re.compile(r"wp-block-file")):
        a = div.find("a", href=re.compile(r"\.pdf", re.I))
        if a:
            return a["href"]
    # Any <a href="...pdf">
    a = soup.find("a", href=re.compile(r"\.pdf", re.I))
    if a:
        return a["href"]
    # WordPress Download Manager button
    btn = soup.find("a", class_=re.compile(r"wpdm-download-link"))
    if btn:
        return btn.get("href")
    return None


def get_category(soup):
    li = soup.find("li", class_="meta-cat")
    if li:
        a = li.find("a")
        return a.get_text(strip=True) if a else li.get_text(strip=True).replace("Post category:", "").strip()
    return "Uncategorised"


def write_metadata(report_dir, d):
    with open(os.path.join(report_dir, "metadata.txt"), "w", encoding="utf-8") as f:
        f.write(f"report-name: {d['title']}\n")
        f.write(f"date:        {d['date']}\n")
        f.write(f"category:    {d['category']}\n")


def download_pdf(pdf_url, save_path):
    if os.path.exists(save_path):
        print(f"      → already downloaded, skipping")
        return True
    for attempt in range(1, MAX_RETRIES + 1):
        try:
            r = requests.get(pdf_url, headers=HEADERS, stream=True, timeout=60)
            r.raise_for_status()
            with open(save_path, "wb") as f:
                for chunk in r.iter_content(chunk_size=8192):
                    f.write(chunk)
            kb = os.path.getsize(save_path) // 1024
            print(f"      → PDF saved ({kb} KB)")
            return True
        except requests.exceptions.RequestException as exc:
            print(f"      [retry {attempt}/{MAX_RETRIES}] PDF error: {exc}")
            if attempt < MAX_RETRIES:
                time.sleep(attempt * 2)
    print(f"      ✗ PDF download failed")
    return False


def process_report(url, idx, total):
    """Fetch detail page, save metadata.txt and PDF."""
    print(f"    [{idx}/{total}] {url}")

    r = get_page(url)
    if r is None:
        return False

    soup = BeautifulSoup(r.content, "html.parser")

    # Title
    title_tag = (soup.find("h2", class_="single-post-title")
                 or soup.find("h1", class_="entry-title")
                 or soup.find("h2", class_="entry-title"))
    title = title_tag.get_text(strip=True) if title_tag else "Unknown"

    dt, slug = parse_url_parts(url)

    if dt:
        date_str   = dt.strftime("%d/%m/%Y")
        month_year = dt.strftime("%m-%Y")
    else:
        # fallback to meta bar date
        li = soup.find("li", class_="meta-date")
        raw = li.get_text(strip=True).replace("Post published:", "").strip() if li else ""
        try:
            dt = datetime.strptime(raw, "%B %d, %Y")
            date_str, month_year = dt.strftime("%d/%m/%Y"), dt.strftime("%m-%Y")
        except ValueError:
            date_str, month_year = raw or "unknown", "unknown"

    category = get_category(soup)
    pdf_url  = find_pdf_url(soup)

    # Create folder
    report_dir = os.path.join(OUTPUT_DIR, month_year, slug)
    os.makedirs(report_dir, exist_ok=True)

    # Save metadata
    write_metadata(report_dir, {
        "slug": slug, "title": title, "date": date_str,
        "category": category, "url": url, "pdf_url": pdf_url,
    })
    print(f"      → metadata.txt saved  [{category}]  {date_str}")

    # Download PDF
    if pdf_url:
        pdf_name = os.path.basename(urlparse(pdf_url).path)
        if not pdf_name.lower().endswith(".pdf"):
            pdf_name = f"{slug}.pdf"
        download_pdf(pdf_url, os.path.join(report_dir, pdf_name))
    else:
        print(f"      ⚠  No PDF found on this page")

    return True


# ── Main ──────────────────────────────────────────────────────────────────────

def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    print("=" * 65)
    print("  Bridgepath Capital Reports Scraper  (month-by-month)")
    print(f"  Starting : {START_YEAR}/{START_MONTH:02d}  →  going backward")
    print(f"  Output   : {os.path.abspath(OUTPUT_DIR)}/")
    print("=" * 65)

    year, month   = START_YEAR, START_MONTH
    empty_streak  = 0
    grand_total   = 0
    grand_ok      = 0

    while True:
        label = f"{year}/{month:02d}"
        bar   = "─" * (62 - len(label))
        print(f"\n┌── {label} {bar}")

        # ── A: Discover ──────────────────────────────────────────
        report_urls = discover_month(year, month)
        count = len(report_urls)

        if count == 0:
            empty_streak += 1
            print(f"│   No reports found.  (empty streak: {empty_streak}/{MAX_EMPTY_MONTHS})")
            print(f"└{'─'*63}")
            if empty_streak >= MAX_EMPTY_MONTHS:
                print(f"\n  {MAX_EMPTY_MONTHS} consecutive empty months – finished.")
                break
        else:
            empty_streak = 0
            print(f"│   Found {count} report(s) – downloading now …")
            print(f"│")

            # ── B: Download each report ──────────────────────────
            ok = 0
            for idx, url in enumerate(report_urls, 1):
                success = process_report(url, idx, count)
                if success:
                    ok += 1
                time.sleep(REQUEST_DELAY)

            grand_total += count
            grand_ok    += ok
            print(f"│")
            print(f"│   {ok}/{count} downloaded successfully.")
            print(f"└{'─'*63}")

        # Stop at 2019 (site didn't exist before ~2020)
        year, month = prev_month(year, month)
        if year < 2019:
            print("\n  Reached 2019 – finished.")
            break

        time.sleep(REQUEST_DELAY)

    print("\n" + "=" * 65)
    print(f"  All done.  {grand_ok}/{grand_total} reports saved.")
    print(f"  Location : {os.path.abspath(OUTPUT_DIR)}/")
    print("=" * 65)


if __name__ == "__main__":
    main()