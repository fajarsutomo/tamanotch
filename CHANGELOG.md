# Changelog

Format mengikuti [Keep a Changelog](https://keepachangelog.com/id-ID/1.1.0/). Basis fork: DRANOTCH v1.8.4.

## [Belum dirilis]

### Ditambahkan
- Mode idle baru: kapsul ramping menempel di tepi atas layar dengan tepi biru muda neon bercahaya dan animasi dua mata yang berkedip dan melirik (berhenti otomatis saat tidak terlihat atau animasi sistem dimatikan).
- Pengaturan posisi horizontal notch (-50% sampai +50% lebar layar) dengan tombol kembali ke tengah.
- Pengaturan ukuran mata saat idle (24 sampai 48 px).
- Pencarian lokasi sampai level kelurahan/desa/kecamatan di Pengaturan untuk jadwal sholat (dan cuaca bila disinkronkan), serta tampilan lokasi aktif beserta koordinatnya.
- Bagian "Tentang TAMANOTCH" di Pengaturan.
- `LICENSE` (MIT, atribusi ke penulis DRANOTCH) dan README baru.

### Diubah
- Proyek diganti nama dari DRANOTCH menjadi TAMANOTCH (kode, installer, skrip, dokumentasi, URL repo). Folder data pengguna menjadi `%LOCALAPPDATA%\TAMANOTCH`; nama mutex, pipe, entri autostart, dan folder Google Drive ikut berganti.
- Klik ikon tengah notch kini membuka Pengaturan.
- Fallback sampul album memakai ikon nada musik.
- Dropdown kota sholat kosong bila lokasi aktif bukan salah satu kota di daftar (sebelumnya menampilkan Jakarta).

### Diperbaiki
- Lokasi sholat dan cuaca tidak bisa diganti: pilihan kota kini menyelaraskan sholat, kota BMKG, dan kode ADM4; data cuaca lama tidak lagi menimpa kota yang baru dipilih; pengingat azan ikut disegarkan; refresh cuaca tidak dibuang saat lokasi berubah di tengah permintaan.
- Jam batas waktu to-do tampil `18:15` di semua locale (sebelumnya `18.15` di locale Indonesia).
- Jendela Spotify: tombol tutup tidak lagi terpotong di lebar sempit, dan halaman diperkecil otomatis agar tombol Log in terjangkau.

### Dihapus
- Ikon/logo DRANOTCH dari dalam island (ikon aplikasi untuk exe/taskbar/installer belum diganti).
- Tombol panah minimize notch ke bezel (▲/▼) beserta entri menu klik kanan, dan status minimize lama diabaikan.
- Jendela kartu "Tentang" (diganti bagian di Pengaturan).
