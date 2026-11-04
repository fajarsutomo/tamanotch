# Changelog

Format mengikuti [Keep a Changelog](https://keepachangelog.com/id-ID/1.1.0/).

## Penomoran versi

TAMANOTCH memakai versi `x.y` dengan aturan yang sama sebelum dan sesudah rilis:

- **x** (angka tengah) naik untuk fitur atau elemen baru yang cukup signifikan.
- **y** (angka akhir) naik untuk perbaikan bug (fitur lama maupun baru) dan kembali ke 0 setiap x naik.

Angka pertama: `0` selama belum ada rilis (`0.x.y`, versi pengembangan, dimulai dari `0.1.0`); aturan x/y di atas berlaku juga selama pengembangan. Rilis pertama dengan installer (Inno Setup) memakai `1.0.0`, lalu berlanjut sebagai `1.x.y`. Versi dasar DRANOTCH hanya dicatat sebagai atribusi (lihat README dan LICENSE).

## [0.7.0] - belum dirilis

### Ditambahkan
- **Kualitas udara (US AQI)** di pill cuaca island, berwarna menurut kategori (Baik, Sedang, Tidak sehat bagi kelompok sensitif, dan seterusnya). Sumbernya Open-Meteo Air Quality API (gratis, tanpa kunci, lisensi data CC-BY 4.0, untuk pemakaian non-komersial): estimasi model CAMS untuk koordinat lokasi aktif, bukan pembacaan stasiun BMKG. Disegarkan tiap 30 menit dan saat lokasi berganti; disembunyikan bila datanya lebih tua dari 2 jam. Tooltip menampilkan PM2.5 dan PM10. Dapat dimatikan di Pengaturan (bagian Cuaca & Gempa Bumi).

## [0.6.0] - belum dirilis

### Diubah
- Lencana gempa terbaru (M x.x) di samping suhu pada island hilang setelah lebih dari 3 jam sejak kejadian; data tetap tampil di jendela Cuaca & Gempa.
- Pengingat sholat/adzan (sebelum, saat, dan sesudah waktu sholat) kini juga tampil sebagai **notch notifikasi berframe hijau** (mata idle diganti satu baris pesan, klik menutup, sorot kursor menampilkan banner penuh), dan notifikasi tray Windows di kanan bawah untuk pengingat sholat dihapus. Bila peringatan BMKG berat aktif bersamaan, notch merah didahulukan. Notch notifikasi (hijau maupun merah) tetap menempel di tepi kanan notch idle dan melebar ke kiri.

## [0.5.0] - belum dirilis

### Diubah
- Peringatan BMKG berat (gempa M >= 5 atau berpotensi tsunami, cuaca ekstrem) kini tampil sebagai **notch notifikasi**: mata idle diganti satu baris pesan dengan tinggi yang sama, lebar mengikuti panjang pesan, dan frame merah; klik membuka detail, sorot kursor menampilkan banner penuh. Notifikasi tray Windows di kanan bawah untuk peringatan BMKG dihapus. Gempa lebih kecil tetap memakai banner island.

## [0.4.1] - belum dirilis

### Diperbaiki
- Tombol tutup (✕) jendela YouTube Music terpotong pada ukuran HP: di lebar sempit, chip "Geser", judul, dan tombol maximize disembunyikan seperti pada Spotify.

## [0.4.0] - belum dirilis

### Ditambahkan
- **DNS-over-HTTPS (DoH) untuk jendela web** (opsional, bawaan mati) di Pengaturan: jendela Jellyfin, Spotify, YouTube Music, Live TV, dan browser me-resolve alamat https lewat Cloudflare atau Google DoH (IP literal, cadangan otomatis ke penyedia lain, cache sesuai TTL) memakai proxy CONNECT lokal di 127.0.0.1 tanpa intersepsi TLS, sehingga berguna saat DNS jaringan (mis. kantor) memblokir atau salah menjawab alamat. DNS dan jaringan Windows tidak diubah, tidak ada kembali diam-diam ke DNS Windows (kegagalan dilaporkan), hanya CONNECT port 443 yang diterima. Berlaku setelah aplikasi dimulai ulang; log di `%LOCALAPPDATA%\TAMANOTCH\doh.log`.

## [0.3.0] - belum dirilis

### Ditambahkan
- Pemutar **Jellyfin** (server media pribadi, bawaan `myflix.tamahome.id`) di jendela WebView2 seperti YouTube Music dan Spotify: login tersimpan, tombol segitiga di notch dan menu tray, saling jeda dengan YouTube Music, Spotify, radio, dan Live TV, selalu-di-atas, ukuran HP, dan mode tampilan HP (layout mobile Jellyfin). Alamat server dan tombol "Tes koneksi" ada di Pengaturan.

## [0.2.0] - belum dirilis

### Ditambahkan
- Ikon aplikasi baru (taskbar, tray, installer, dan jendela): kotak gelap bertepi neon dengan dua mata menyala, turunan dari notch idle. Dibuat dari kode oleh `scripts/make-icon.ps1` sehingga dapat dibuat ulang dengan warna lain.

## [0.1.0] - belum dirilis

### Ditambahkan
- Pilihan ukuran HP di menu ukuran (ikon 📐) jendela YouTube Music dan Spotify: iPhone SE, Galaxy S, iPhone 14/15, Pixel 7, iPhone Pro Max, serta tombol putar (tukar lebar dan tinggi). Area web dibuat seukuran layar HP, dan ukuran diperkecil otomatis bila melebihi layar.
- Kustomisasi notch di Pengaturan: warna mata dan warna frame saat idle dari 11 preset neon, bentuk frame idle (panjang, lebar, dan radius sudut kiri-bawah dan kanan-bawah, atau otomatis mengikuti ukuran mata), serta warna frame island dan warna cincin ikon saat disorot kursor (dengan opsi "Samakan dengan notch idle" dan pilihan kembali ke tampilan bawaan).
- Pengingat kesehatan (opt-in) di Pengaturan: pengingat gerak (bawaan tiap 45 menit) dan minum (bawaan tiap 60 menit, target 8 gelas x 230 ml dengan penghitung harian). Hanya pada jam kerja, tidak saat Anda sedang pergi (tanpa input >= 2 menit dihitung jeda gerak), pengingat minum berhenti setelah target tercapai. Tampil sebagai banner island dengan aksi "Sudah bergerak/minum", tunda 10 menit, dan tutup.
- Mode idle baru: kapsul ramping menempel di tepi atas layar dengan tepi biru muda neon bercahaya dan animasi dua mata yang berkedip dan melirik (berhenti otomatis saat tidak terlihat atau animasi sistem dimatikan).
- Pengaturan posisi horizontal notch (-50% sampai +50% lebar layar) dengan tombol kembali ke tengah.
- Pengaturan ukuran mata saat idle (24 sampai 48 px).
- Pencarian lokasi sampai level kelurahan/desa/kecamatan di Pengaturan untuk jadwal sholat (dan cuaca bila disinkronkan), serta tampilan lokasi aktif beserta koordinatnya.
- Bagian "Tentang TAMANOTCH" di Pengaturan.
- `LICENSE` (MIT, atribusi ke penulis DRANOTCH) dan README baru.

### Diubah
- Daftar bawaan stasiun radio, saluran TV, negara Live TV, dan kota kini berkas JSON di `Assets/Data` (dapat diubah tanpa kompilasi ulang); berkas yang hilang atau rusak tidak membuat aplikasi gagal.
- Proyek diganti nama dari DRANOTCH menjadi TAMANOTCH (kode, installer, skrip, dokumentasi, URL repo). Folder data pengguna menjadi `%LOCALAPPDATA%\TAMANOTCH`; nama mutex, pipe, entri autostart, dan folder Google Drive ikut berganti.
- Klik ikon tengah notch kini membuka Pengaturan.
- Fallback sampul album memakai ikon nada musik.
- Dropdown kota sholat kosong bila lokasi aktif bukan salah satu kota di daftar (sebelumnya menampilkan Jakarta).

### Diperbaiki
- Lokasi sholat dan cuaca tidak bisa diganti: pilihan kota kini menyelaraskan sholat, kota BMKG, dan kode ADM4; data cuaca lama tidak lagi menimpa kota yang baru dipilih; pengingat azan ikut disegarkan; refresh cuaca tidak dibuang saat lokasi berubah di tengah permintaan.
- Jam batas waktu to-do tampil `18:15` di semua locale (sebelumnya `18.15` di locale Indonesia).
- Jendela Spotify: tombol tutup tidak lagi terpotong di lebar sempit, dan halaman diperkecil otomatis agar tombol Log in terjangkau.

### Dihapus
- Mode "mini idle" dan setelan `ExpandOnHover` (sudah tidak aktif sebelumnya), serta properti dan perintah yang tidak lagi terpakai di ViewModel.
- Ikon/logo DRANOTCH dari dalam island (ikon aplikasi untuk exe/taskbar/installer belum diganti).
- Tombol panah minimize notch ke bezel (▲/▼) beserta entri menu klik kanan, dan status minimize lama diabaikan.
- Jendela kartu "Tentang" (diganti bagian di Pengaturan).
