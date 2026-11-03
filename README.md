<div align="center">

# TAMANOTCH

### Dynamic Island & pendamping produktivitas untuk Windows

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=for-the-badge&logo=windows)](https://github.com/fajarsutomo/tamanotch)
[![.NET](https://img.shields.io/badge/.NET-10.0%20WPF-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

</div>

TAMANOTCH adalah widget *Dynamic Island* di bagian atas layar Windows. Saat idle ia berupa kapsul kecil bertepi biru neon dengan sepasang mata yang berkedip. Saat kursor mendekat, kapsul melebar menjadi island berisi komponen-komponen berikut.

> Proyek ini adalah **fork pribadi** dari [DRANOTCH](https://github.com/dningratx/dranotch) karya **dningratx** (lisensi MIT), dikembangkan terpisah dengan nama, tampilan, dan perbaikan sendiri. Lihat [Kredit & Lisensi](#kredit--lisensi).

## Fitur

| Komponen | Kemampuan |
|---|---|
| **Pemutar musik & lirik** | Mendeteksi lagu dari Windows Media Session; pemutar bawaan YouTube Music dan Spotify (WebView2, login tersimpan, saling jeda otomatis); lirik tersinkron dari [LRCLIB](https://lrclib.net) dengan offset manual dan impor `.lrc`/`.txt`; mode karaoke (penghilang vokal) |
| **Radio online** | Puluhan stasiun Indonesia dan dunia, pencarian stasiun lewat Radio-Browser, favorit, stasiun kustom |
| **Live TV** | Saluran TV Indonesia dan dunia (HLS dan YouTube Live), failover sumber, mode bioskop, mini PiP |
| **Waktu sholat** | Jadwal offline (standar Kemenag/MABIMS), pilihan kota, pencarian sampai kelurahan/desa, pengingat dan azan dengan auto-pause musik |
| **Cuaca & gempa BMKG** | Prakiraan 3 hari dan informasi gempa dari data terbuka BMKG, lokasi presisi tingkat kelurahan/desa, peringatan dini |
| **To-do** | Kategori bercabang, drag and drop, deadline dengan pengingat, jendela lepas, sinkronisasi Google Drive |
| **Browser mengambang** | Jendela web bebas dengan fullscreen dalam jendela, preset ukuran, selalu di atas |
| **Pengingat kesehatan** | Pengingat gerak dan minum selama jam kerja (interval, target gelas, jam kerja bisa diatur), penghitung gelas harian, jeda otomatis saat Anda pergi dari komputer |
| **Pengaturan notch** | Geser posisi kiri/kanan, ukuran mata idle, hotkey, pilihan komponen |

Pintasan bawaan: <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>N</kbd> tampil/sembunyi panel, <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>L</kbd> mode lirik.

## Pemasangan

Belum ada rilis terpasang. Untuk sementara jalankan dari source (lihat di bawah).

## Menjalankan dari source

Prasyarat: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) dan Microsoft Edge WebView2 Runtime (sudah ada di Windows 11).

```bash
git clone https://github.com/fajarsutomo/tamanotch.git
cd tamanotch
dotnet run --project src/NotchMusic        # jalankan tanpa build installer
dotnet test                                 # tes unit
```

Installer (opsional, butuh [Inno Setup 6](https://jrsoftware.org/isdl.php)):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Hasilnya ada di `dist/Publish/` (portabel) dan `dist/Installer/`. Data pengguna disimpan di `%LOCALAPPDATA%\TAMANOTCH`.

### Sinkronisasi Google Drive

Sinkronisasi to-do memakai kredensial OAuth Google. Buat kredensial sendiri mengikuti [docs/GOOGLE_DRIVE_SETUP.md](docs/GOOGLE_DRIVE_SETUP.md) lalu impor lewat Pengaturan. Aplikasi hanya meminta akses `drive.file` (berkas yang dibuatnya sendiri).

## Layanan dan data pihak ketiga

BMKG (cuaca dan gempa), LRCLIB (lirik), Radio-Browser (stasiun radio), serta stream radio/TV publik. Ketersediaan dan syarat pemakaian mengikuti masing-masing penyedia.

## Kredit & Lisensi

- Berlisensi [MIT](LICENSE).
- Dikembangkan berdasarkan **DRANOTCH v1.8.4** oleh [dningratx](https://github.com/dningratx/dranotch) (MIT). Hak cipta kode asli tetap milik penulis aslinya; modifikasi TAMANOTCH oleh Fajar Sutomo.
- Riwayat perubahan ada di [CHANGELOG.md](CHANGELOG.md).
