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
| **Jellyfin** | Pemutar server media Jellyfin pribadi di jendela WebView2 (login tersimpan, tampilan HP, saling jeda dengan pemutar lain); alamat server diatur di Pengaturan |
| **Al-Qur'an** | Pemutar murottal 114 surah (Mishari Alafasy, Quran.com API) dengan pencarian, surah acak, progres yang bisa diklik, dan mode mini; tanpa teks ayat |
| **Radio online** | Puluhan stasiun Indonesia dan dunia, pencarian stasiun lewat Radio-Browser, favorit, stasiun kustom |
| **Live TV** | Saluran TV Indonesia dan dunia (HLS dan YouTube Live), failover sumber, mode bioskop, mini PiP |
| **Waktu sholat** | Jadwal offline (standar Kemenag/MABIMS), pilihan kota, pencarian sampai kelurahan/desa, pengingat sebelum, saat, dan sesudah azan di notch dengan auto-pause musik; mode puasa (pengingat sahur, notifikasi berbuka, pengingat minum diam selama berpuasa) |
| **Cuaca & gempa BMKG** | Prakiraan 3 hari dan informasi gempa dari data terbuka BMKG, lokasi presisi tingkat kelurahan/desa, peringatan dini |
| **To-do** | Kategori bercabang, drag and drop, deadline dengan pengingat, jendela lepas, sinkronisasi Google Drive |
| **Browser mengambang** | Jendela web bebas dengan fullscreen dalam jendela, preset ukuran, selalu di atas |
| **Pengingat kesehatan** | Pengingat gerak dan minum selama jam kerja (interval, target gelas, jam kerja bisa diatur), penghitung gelas harian yang bisa ditambah langsung dengan klik di notch, jeda otomatis saat Anda pergi dari komputer |
| **Pemakaian Claude** | Memantau kuota paket Claude Pro/Max (sesi 5 jam dan mingguan 7 hari) di jendela sendiri, lencana di island, dan notifikasi tray. Mati secara bawaan; lihat catatan di bawah |
| **Sapaan** | "Selamat datang kembali, <nama>" di notch saat Anda memakai komputer lagi setelah lama pergi (idle, terkunci, atau sleep); nama dan ambang idle diatur di Pengaturan |
| **Pengaturan & tema** | Pengaturan bersidebar; tema Gelap, Putih, dan Liquid Glass; geser posisi notch kiri/kanan, ukuran mata idle, hotkey, pilihan komponen |

Pintasan bawaan: <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>N</kbd> tampil/sembunyi panel, <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>L</kbd> mode lirik.

## Pemasangan

Unduh installer dari halaman [Releases](https://github.com/fajarsutomo/tamanotch/releases/latest) (Windows 10/11, x64, tanpa hak admin). Installer ditandatangani sertifikat buatan sendiri, jadi SmartScreen bisa memperingatkan; jalankan `Install-Certificate.bat` yang terlampir lebih dulu atau pilih "Run anyway". Atau jalankan dari source (lihat di bawah).

## Menjalankan dari source

Prasyarat: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) dan Microsoft Edge WebView2 Runtime (sudah ada di Windows 11).

```bash
git clone https://github.com/fajarsutomo/tamanotch.git
cd tamanotch
dotnet run --project src/TamaNotch        # jalankan tanpa build installer
dotnet test                                 # tes unit
```

Installer (opsional, butuh [Inno Setup 6](https://jrsoftware.org/isdl.php)):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Hasilnya ada di `dist/Publish/` (portabel) dan `dist/Installer/`. Data pengguna disimpan di `%LOCALAPPDATA%\TAMANOTCH`.

### Sinkronisasi Google Drive

Sinkronisasi to-do memakai kredensial OAuth Google yang **tidak disertakan** di aplikasi: Anda membuatnya sendiri (gratis, sekali saja) mengikuti [docs/GOOGLE_DRIVE_SETUP.md](docs/GOOGLE_DRIVE_SETUP.md), lalu mengimpor `credentials.json` atau mengisi Client ID dan Secret di Pengaturan. Aplikasi hanya meminta akses `drive.file` (berkas yang dibuatnya sendiri).

### Pemakaian Claude

Fitur ini membaca token login Claude Code yang sudah ada di komputer Anda (hanya dibaca, tidak pernah diperbarui atau disalin) dan memanggil layanan usage Anthropic yang **tidak terdokumentasi resmi**. Pemakaian token langganan oleh aplikasi pihak ketiga bisa tidak sejalan dengan ketentuan Anthropic, dan layanannya bisa berubah kapan saja. Karena itu fitur ini mati secara bawaan dan baru aktif setelah Anda menyetujuinya di Pengaturan.

## Layanan dan data pihak ketiga

BMKG (cuaca dan gempa), LRCLIB (lirik), Radio-Browser (stasiun radio), Quran.com (audio murottal), Google Drive (sinkronisasi to-do, opsional), layanan usage Anthropic (Pemakaian Claude, opsional), serta stream radio/TV publik. Ketersediaan dan syarat pemakaian mengikuti masing-masing penyedia.

## Kredit & Lisensi

- Berlisensi [MIT](LICENSE).
- Ikon antarmuka berasal dari [Material Design Icons](https://github.com/Templarian/MaterialDesign) / Google Material Icons, lisensi Apache-2.0.
- Dikembangkan berdasarkan **DRANOTCH v1.8.4** oleh [dningratx](https://github.com/dningratx/dranotch) (MIT). Hak cipta kode asli tetap milik penulis aslinya; modifikasi TAMANOTCH oleh Fajar Sutomo.
- Riwayat perubahan ada di [CHANGELOG.md](CHANGELOG.md).
