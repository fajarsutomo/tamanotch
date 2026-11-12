# Changelog

Format mengikuti [Keep a Changelog](https://keepachangelog.com/id-ID/1.1.0/).

## Penomoran versi

TAMANOTCH memakai versi `x.y` dengan aturan yang sama sebelum dan sesudah rilis:

- **x** (angka tengah) naik untuk fitur atau elemen baru yang cukup signifikan.
- **y** (angka akhir) naik untuk perbaikan bug (fitur lama maupun baru) dan kembali ke 0 setiap x naik.

Angka pertama: `0` selama belum ada rilis (`0.x.y`, versi pengembangan, dimulai dari `0.1.0`); aturan x/y di atas berlaku juga selama pengembangan. Rilis pertama dengan installer (Inno Setup) memakai `1.0.0`, lalu berlanjut sebagai `1.x.y`. Versi dasar DRANOTCH hanya dicatat sebagai atribusi (lihat README dan LICENSE).

## [1.7.0] - 2026-11-12

### Ditambahkan
- **Pengaturan > Tema > Taskbar Transparan**: opsi "Jalankan TranslucentTB bersama TAMANOTCH". Taskbar Windows 11 terbaru digambar XAML dan tidak bisa dibuat transparan lewat API biasa (diuji: perintah diterima tetapi tidak berefek), jadi TAMANOTCH tidak mengubah taskbar sendiri. Ia hanya menyalakan TranslucentTB (bila terpasang dan belum berjalan) setiap kali TAMANOTCH start, sehingga autostart TranslucentTB yang tidak andal tidak lagi dibutuhkan. Status terpasang/berjalan ditampilkan di bawah opsi.

## [1.6.1] - 2026-11-12

### Diperbaiki
- **Jendela Pengaturan di tema Liquid Glass lebih sesuai dengan island**: latar akrilik kini berlapis gradasi grafit netral yang sedikit lebih terang di tepi atas, dan garis tepi jendela diganti abu-abu terang. Kotak teks (mis. "Nama Anda") yang sebelumnya putih polos di semua tema kini mengikuti tema.

## [1.6.0] - 2026-11-12

### Ditambahkan
- **Pengaturan > Tema > Liquid Glass Lanjutan** (aktif saat tema Liquid Glass; perubahan langsung terlihat):
  - *Blur latar island* (nyala/mati). Saat mati, island menjadi kaca polos tanpa blur dan kembali terlihat di screenshot, rekaman, dan berbagi layar.
  - *Kekuatan blur* 1-10 (bawaan 4).
  - *Warna kaca island* 0-80% (bawaan 15%); tombol dan pil di dalam island tidak terpengaruh.
  - *Sesuaikan otomatis dengan terang latar*: kaca menebal sampai 35 poin di atas latar terang agar teks putih terbaca.
  - *Pembaruan latar* 100-1000 ms (bawaan 300 ms).
  - *Blur jendela lain* (Radio, Al-Qur'an, BMKG, Pengaturan, dan seterusnya).
  - Tombol *Kembalikan ke Bawaan*.
- **Warna uptime** di pil island dan panel monitor: hijau neon di bawah 1 minggu, kuning 1-2 minggu, oranye neon 2-3 minggu, merah neon 3 minggu ke atas (tooltip menyarankan restart). Di tema Putih memakai versi lebih gelap agar terbaca di pil putih.

### Diperbaiki
- Bentuk island "tetes air" tidak lagi kehilangan warnanya setelah keluar dari tema Liquid Glass.

## [1.5.2] - 2026-11-12

### Diubah
- **Sticky note selebar island**: tab Akun menyusun kartu 4 kolom (masing-masing 25%), tab Catatan 2 kolom (50%), mengisi dari kiri ke kanan lalu ke baris berikutnya. Kartu akun kini bersusun (judul, user, password) agar muat di kolom sempit.
- **Monitor sistem selebar island** dengan 5 kartu sejajar (masing-masing 20%): CPU, memori, disk, jaringan, GPU; nilai di atas, grafik di bawah. Garis bantu grafik kini horizontal (25/50/75/100%), seperti skala persen Task Manager.
- **Sidebar Pengaturan bergambar**: tiap kategori punya ikon Material.
- **Penanda menu aktif kini bingkai hijau** pada tombol island (sebelumnya titik hijau kecil di pojok): musik/radio/Al-Qur'an sedang diputar, Live TV/browser/BMKG terbuka, pemantauan Claude aktif, dan (baru) panel sticky note atau monitor sistem sedang terbuka.
- **Ikon island benar-benar flat**: ikon komponen (musik, radio, Live TV, Al-Qur'an, Claude, browser, BMKG, to-do, sholat) diganti ikon Material berisi warna polos; gradien logam pada ikon, lencana, pil, dan bingkai island dihapus di ketiga tema. Geometri ikon disatukan di `Themes/Icons.xaml` (sebelumnya disalin di beberapa jendela).

### Diperbaiki
- **Tema Liquid Glass tidak lagi terlihat seperti tema gelap.** Tampilannya meniru Liquid Glass macOS: kaca netral bening (bukan biru) di atas blur latar yang sungguhan.
  - **Island**: latarnya dibuat dari salinan layar di belakangnya yang diperkecil dan di-blur (diperbarui tiap 0,3 detik), lalu dipakai sebagai kuas latar island. Island tetap menempel ke tepi atas layar, bentuk "tetes air" dengan lekukannya tetap ada, animasi buka/menciut tetap jalan, dan island jauh lebih bening (warna kaca 15% di atas latar gelap, naik sampai sekitar 50% di atas latar terang agar teks putih terbaca) sementara tombol dan pil tetap setransparan sebelumnya. Supaya island tidak ikut tersalin, island dikecualikan dari tangkapan layar selama tema Glass: **island tidak tampak di screenshot, rekaman, atau berbagi layar** (Teams/Zoom) saat tema Glass aktif.
  - **Jendela transparan** (Radio, Al-Qur'an, Sholat, Claude, Musik, To-Do, BMKG, Live TV): blur desktop bawaan Windows dengan sudut 8 px, tepi putih berkilau, tanpa margin bayangan. **Pengaturan** dan jendela biasa: backdrop Acrylic Windows 11 (build 22621+) dengan lapisan grafit agar teks putih terbaca.
  - Di tema lain semua efek dilepas dan tampilan kembali seperti semula.
- **Notifikasi WhatsApp muncul sebagai lagu di pemutar musik.** WhatsApp Web (dan situs lain seperti Teams/Gmail) mendaftarkan bunyi notifikasinya ke Windows sebagai sesi media berjudul judul halaman, mis. "(5) WhatsApp" berdurasi 2 detik. Sesi dengan durasi di bawah 15 detik kini diabaikan saat memilih sesi musik (kecuali dipilih manual), dan tidak menggeser sesi musik yang sedang dipakai.
- **Bentuk island "tetes air" selalu hitam** di semua tema; kini mengikuti warna island tema (`ThemeIslandIdleBg`).
- **Aplikasi gagal dibuka (XamlParseException `DynamicResource` pada `TargetNullValue`)** setelah migrasi tema; nilai cadangan binding kembali ke `StaticResource`. Tes baru mencegah `DynamicResource` di dalam binding.

## [1.5.1] - 2026-11-12

### Diubah
- **Proyek diganti nama dari NotchMusic menjadi TamaNotch**: folder `src/TamaNotch` dan `tests/TamaNotch.Tests`, `TamaNotch.csproj`, `TamaNotch.slnx`, `Installer/TamaNotch.iss`, namespace `TamaNotch.*`, dan berkas program `TamaNotch.exe` (sebelumnya `NotchMusic.exe`; nama proses di Task Manager ikut berubah). Skrip `scripts/*.ps1` dan README mengikuti. Tidak ada perubahan perilaku aplikasi.
- Installer: `AppId` tetap sehingga pembaruan menimpa instalasi lama; sisa `NotchMusic.*` di folder pemasangan dibuang saat memperbarui, dan proses `NotchMusic.exe` yang masih berjalan ikut ditutup.

### Diperbaiki
- **Autostart tidak lagi menunjuk `NotchMusic.exe` yang sudah tidak ada setelah pembaruan.** Saat aplikasi mulai, nilai Run `TAMANOTCH` ditulis ulang bila menunjuk exe lama atau berkas yang hilang. Nilai yang menunjuk exe lain yang masih ada dibiarkan, jadi menjalankan versi dev tidak menimpa autostart versi terpasang.

Folder data `%LOCALAPPDATA%\TAMANOTCH`, mutex, pipe, nama registry, dan nama produk "TAMANOTCH" tidak berubah (tanpa migrasi data).

## [1.5.0] - 2026-11-12

### Ditambahkan
- **Monitor sistem** (ikon chip di baris tombol island; aktif bawaan). Panel tertanam di island berisi grafik gaya Task Manager untuk **CPU, memori** (terpakai / total), **disk** (aktivitas), **jaringan** (unduh dan unggah) dan **GPU** (3D), masing-masing 60 titik terakhir dengan nilai terkini, plus uptime lengkap (hari:jam:menit:detik). Pembacaan memakai Windows langsung tanpa paket tambahan (GetSystemTimes, GlobalMemoryStatusEx, statistik adapter, dan PDH untuk disk/GPU) dan hanya berjalan selama panelnya terbuka. Kartu disk atau GPU disembunyikan bila Windows tidak menyediakan datanya.
- **Pil uptime** di baris info island, sebaris dengan sholat, cuaca, dan Claude: "Up 0:14:45" (hari:jam:menit sejak Windows menyala, sama dengan Up time di Task Manager); klik membuka monitor sistem. Diperbarui tiap 30 detik selama island terbuka.
- Pengaturan > Sticky Note & Monitor: `EnableSystemMonitor` dan `ShowUptimeInIsland` (keduanya menyala bawaan).

## [1.4.0] - 2026-11-12

### Ditambahkan
- **Sticky note** (ikon catatan di baris tombol island; aktif bawaan, bisa dimatikan di Pengaturan > Sticky Note & Monitor). Panel tertanam di island dengan dua tab: **Akun** (judul, user, password yang disamarkan dengan tombol tampil/sembunyi, tombol salin per kolom; mis. login SAP) dan **Catatan** (teks bebas bertitel dengan tombol salin). Tambah dan hapus lewat tombol di panel; perubahan disimpan otomatis saat kotak teks kehilangan fokus atau panel ditutup. Disimpan di `%LOCALAPPDATA%\TAMANOTCH\notes.json` sebagai **teks biasa tanpa enkripsi** dan tanpa sinkronisasi Drive (sengaja, untuk akun intranet; Pengaturan menampilkan peringatan dan lokasi berkas). Berkas yang rusak tidak ditimpa: salinannya disimpan sebagai `notes.json.bad`. Pengaturan baru: `EnableNotes`.

## [1.3.0] - 2026-11-12

### Ditambahkan
- **Panel tertanam di island** untuk Radio, Al-Qur'an, Waktu Sholat, dan Pemakaian Claude. Pengaturan > Tampilan Notch > "Tampil di Island atau Jendela" memilih per komponen: jendela sendiri (bawaan) atau tertanam di island. Bila tertanam, menekan ikon komponen di island membuka panelnya di antara baris tombol dan baris info; panel menutup lewat tombol tutup atau otomatis saat island menciut ke kapsul idle (radio dan murottal tetap berjalan). Dari menu tray atau saat island tidak terbuka, komponen tetap dibuka sebagai jendela. Komponen berbasis WebView (Live TV, YouTube Music, Spotify, Jellyfin, Browser) tetap jendela karena island transparan. Pengaturan baru: `RadioDisplayMode`, `QuranDisplayMode`, `PrayerDisplayMode`, `ClaudeUsageDisplayMode`.

### Diubah
- Isi jendela Radio, Al-Qur'an, Sholat, dan Pemakaian Claude diekstrak menjadi UserControl (`RadioPanel`, `QuranPanel`, `PrayerPanel`, `ClaudeUsagePanel`) yang dipakai bersama jendela dan island; jendela kini pembungkus tipis (posisi, geser, animasi). Tampilan jendela tidak berubah.

## [1.2.0] - 2026-11-12

### Ditambahkan
- **Tema tampilan: Gelap, Putih, dan Liquid Glass** (Pengaturan > Tema). Berlaku untuk island, Pengaturan, dan semua jendela (Radio, Al-Qur'an, Sholat, BMKG, To-Do, Musik, Claude, serta jendela web), dan langsung berubah tanpa restart. Warna dipindah dari ~1.000 nilai hard-coded ke kamus semantik `Themes/Colors.Dark|Light|Glass.xaml` (permukaan, garis, teks, island, pil, ikon logam, scrollbar) lewat `ThemeService`. Pengaturan baru: `AppTheme`. Liquid Glass adalah kaca tiruan (permukaan tembus pandang, tepi berkilau) tanpa blur latar; warna aksen dan status (gempa, kuota, AQI) serta neon notch tidak ikut tema.
- **Ikon Material flat** (`MaterialIcons`, ikon vektor 24x24 yang mengikuti warna teks/tema) menggantikan emoji dan simbol teks pada bar judul, tombol navigasi (kembali, maju, beranda, segarkan, pin), kontrol jendela, tombol ciut/lepas, dan lain-lain. Emoji kategori dan teks bergambar tetap.

### Diubah
- Kamus brush bersama (`MetallicSilverBrush`, `LuxuryPillBackgroundBrush`, `NotchPill*`, scrollbar, dll.) kini satu salinan di kamus tema; salinan ganda per jendela dihapus. Beberapa warna abu-abu serupa digabung ke satu tingkatan permukaan, jadi selisih nuansa kecil pada tampilan Gelap mungkin terlihat.
- Tema Putih memetakan teks putih ke teks gelap, termasuk teks putih di atas tombol berwarna; sebagian kecil elemen dengan warna tertanam di kode (mis. isi Live TV) tetap gelap.

## [1.1.0] - 2026-11-12

### Diubah
- **Pengaturan memakai sidebar kategori.** Jendela Pengaturan (kini 780x640) punya daftar kategori di kiri: Umum, Tampilan Notch, Musik & Media, Waktu Sholat, Cuaca & Info, Kesehatan, Sinkronisasi & Jaringan, dan Pembaruan; isi kategori terpilih tampil di kanan. Tidak ada pengaturan yang dihapus atau diganti nama; kartu yang sebelumnya bertumpuk dalam satu halaman panjang kini dikelompokkan per kategori.

## [1.0.2] - 2026-11-12

### Diperbaiki
- **Notch pengingat minum dan gerak tidak bisa diklik.** Begitu kursor menyentuh notch, island terbuka dan notch menghilang sebelum sempat diklik, sehingga "Sudah minum"/"Sudah bergerak" dan menu klik kanan (tunda/tutup) tidak terjangkau. Selama notch pengingat itu tampil, kursor di atasnya tidak lagi membuka island; island terbuka seperti biasa setelah pengingat selesai (diklik, ditutup, atau habis 40 detik). Notch sapaan tetap menghilang saat disentuh kursor.

## [1.0.1] - 2026-11-09

### Diperbaiki
- **Gempa kecil masih tampil sebagai banner island lama.** Hanya gempa M >= 5 atau berpotensi tsunami yang memakai notch, sehingga gempa kecil (mis. M 2.5) tampil di island dan melanggar aturan "tampil di notch berarti tidak di island". Kini semua notifikasi gempa memakai notch, dengan warna bingkai menurut tingkat: kuning (M < 3), oranye (M 3 sampai < 5), merah (M >= 5 atau berpotensi tsunami; cuaca ekstrem tetap merah). Banner island hanya menjadi cadangan saat notifikasi tiba ketika island sedang terbuka.

## [1.0.0] - 2026-11-06

Rilis pertama dengan installer (Inno Setup). Seluruh perubahan 0.1.0 sampai 0.14.2 di bawah ini ikut di dalamnya. Installer tidak membawa kredensial Google apa pun; sinkronisasi to-do memakai kredensial yang dibuat pengguna di Pengaturan. Pemakaian Claude mati secara bawaan dan memakai layanan Anthropic yang tidak terdokumentasi resmi.

## [0.14.2] - belum dirilis

### Diperbaiki
- **Favorit radio dan Live TV dari pencarian/katalog online hilang setelah aplikasi dijalankan ulang.** Yang tersimpan hanya ID-nya, sedangkan stasiun/saluran online tidak dimuat ulang saat start. Kini data lengkapnya (nama, alamat stream, kota, genre) disimpan saat difavoritkan dan dimuat kembali saat start, lalu dibuang saat favoritnya dilepas. Pengaturan baru: `SavedOnlineRadioStations`, `SavedOnlineLiveTvChannels`. Favorit yang ID-nya sudah terlanjur tersimpan tanpa data tidak bisa dipulihkan; cukup cari dan favoritkan lagi.

## [0.14.1] - belum dirilis

### Diubah
- **Kredensial Google Drive tidak lagi disertakan di aplikasi.** Client ID dan secret bawaan (warisan DRANOTCH, memakai project Google Cloud milik penulis aslinya) dihapus dari kode. Sinkronisasi to-do kini memakai kredensial Google yang Anda buat sendiri (lihat `docs/GOOGLE_DRIVE_SETUP.md`), diimpor lewat Pengaturan > To-do atau dibaca dari variabel lingkungan `GOOGLE_DRIVE_CLIENT_ID`/`GOOGLE_DRIVE_CLIENT_SECRET`. Selama belum terisi, bagian kredensial di Pengaturan langsung terbuka dengan petunjuk, dan tombol Hubungkan nonaktif. Installer dengan begitu tidak membawa kredensial apa pun.
- README memuat Al-Qur'an, Pemakaian Claude (dengan catatan layanan tak terdokumentasi), dan Sapaan.

BREAKING CHANGE: pengguna yang sebelumnya tersambung lewat kredensial bawaan harus membuat kredensial sendiri dan menghubungkan ulang akun Google-nya.

## [0.14.0] - belum dirilis

### Ditambahkan
- **Sapaan "Selamat datang kembali, <nama>"** di notch (ungu) begitu ada gerakan mouse atau keyboard lagi setelah idle lama (bawaan 60 menit, bisa 15 sampai 180). Sapaan menyebut waktu (pagi/siang/sore/malam) dan lama Anda pergi, tidak ada kaitannya dengan sholat, dan hanya tampil di notch. Nama diisi di Pengaturan > Sapaan; kosong berarti tanpa nama. Pengaturan baru: `UserDisplayName`, `EnableWelcomeBack`, `WelcomeBackIdleMinutes`.
- **Mode puasa** (Pengaturan > Waktu Sholat): pengingat sahur sebelum Imsak (bawaan 30 menit), notifikasi "Waktunya Berbuka Puasa" saat Maghrib, dan pengingat minum berhenti dari Imsak sampai Maghrib (hitungannya mulai lagi setelah berbuka). Bisa setiap hari (mis. Ramadan) atau hanya Senin dan Kamis. Pengaturan baru: `EnableFasting`, `FastingMondayThursdayOnly`, `FastingSahurMinutes`.
- Pengingat minum dan gerak di notch kini menampilkan petunjuk aksi ("Klik: Sudah minum"); klik di notch menambah hitungan gelas seperti tombol di banner island.

### Diubah
- Semua pengingat sholat (sebelum, saat adzan, sesudah, sahur) hanya tampil di notch dan tidak pernah jadi banner island. Bila notch sedang tidak tampil (kursor di island), pengingat menunggu sampai notch kembali; 20 detik dihitung sejak ia tampil di notch, dan dibuang bila lebih dari 15 menit belum sempat tampil.

### Diperbaiki
- Salah ketik pada pesan pengingat sebelum sholat ("berjamah" menjadi "berjamaah").

## [0.13.0] - belum dirilis

### Ditambahkan
- **Lencana pemakaian Claude** di baris pill island (di samping cuaca): "5j 18% · 7h 9%", tiap angka berwarna sesuai tingkatnya (biru, kuning, merah), tooltip memuat waktu reset, klik membuka jendela Pemakaian Claude. Hanya tampil bila fitur aktif dan angkanya belum lebih dari 30 menit.
- **Notifikasi tray** saat sebuah kuota naik ke tingkat peringatan (75% atau pemakaian jauh mendahului waktu) atau kritis (90%). Satu kali per tingkat per periode reset, jadi tidak berulang tiap penyegaran dan muncul lagi setelah kuota di-reset.
- Pengaturan > Pemakaian Claude: opsi lencana island dan notifikasi tray (keduanya menyala bawaan begitu fitur diaktifkan). Pengaturan baru di `settings.json`: `ShowClaudeUsageInIsland`, `NotifyClaudeUsage`.
## [0.12.0] - belum dirilis

### Ditambahkan
- Jendela **Pemakaian Claude**: memantau kuota paket Claude Pro/Max per **sesi 5 jam** dan per **mingguan (7 hari)** tanpa WebView. Tiap kuota punya bar berwarna (biru, kuning mulai 75%, merah mulai 90% atau saat pemakaian jauh mendahului waktu), persen terpakai, hitung mundur reset, dan garis penanda seberapa jauh periode sudah berjalan. Bila ada, tampil juga kuota mingguan per model (Opus, Sonnet, ...) dan porsi pemakaian per sumber (Claude Code, Chats, Cowork). Dibuka lewat ikon pengukur di island atau menu tray; gaya dan animasi mengikuti jendela Radio/Al-Qur'an.
- Sumber data: endpoint usage milik Claude Code (`api.anthropic.com/api/oauth/usage`) dengan token login Claude Code yang dibaca dari `~/.claude/.credentials.json` (atau `CLAUDE_CONFIG_DIR`). Token hanya dibaca: TAMANOTCH tidak memperbaruinya dan tidak menulis berkas kredensial; token kedaluwarsa memunculkan petunjuk untuk membuka Claude Code sebentar. Polling 2-30 menit (bawaan 3), berhenti sementara saat kena batas laju (mengikuti `Retry-After` atau mundur eksponensial), dan angka lama ditandai "(data lama)" bila gagal menyegarkan.
- **Mati secara bawaan** dan butuh persetujuan: jendela menjelaskan bahwa endpoint ini tidak terdokumentasi resmi dan pemakaian token di aplikasi pihak ketiga bisa tidak sejalan dengan ketentuan Anthropic. Pengaturan > Pemakaian Claude: aktif/nonaktif dan interval segar.
- Pengaturan baru di `settings.json`: `EnableClaudeUsage`, `IsClaudeUsageWindowOpen`, `ClaudeUsageWindowLeft/Top`, `ClaudeUsageRefreshMinutes`.
## [0.11.0] - belum dirilis

### Ditambahkan
- Bentuk **air menggantung** (opsional, Pengaturan > Kustomisasi Notch): sudut atas kiri dan kanan melengkung terbalik (cekung) ke tepi atas layar, sudut bawah tetap seperti biasa. Berlaku untuk idle notch, semua notch notifikasi, dan island hover; mode expanded tidak berubah.
- Slider **Sudut kiri atas** dan **Sudut kanan atas** (0–16 px, bawaan 10 px, mengikuti pengaturan sudut bawah). Bila radius atas dan bawah di satu sisi melebihi tinggi notch, keduanya dikecilkan proporsional.
- Pengaturan baru di `settings.json`: `IdleShape` ("Capsule" bawaan atau "Drop"), `IdleTopRadiusLeft`, `IdleTopRadiusRight`.

## [0.10.0] - belum dirilis

### Diubah
- Pengingat minum dan gerak kini tampil sebagai **notch notifikasi**: minum berframe biru, gerak berframe pink (banner island memakai warna yang sama). Klik notch menjalankan aksi utama ("Sudah minum" atau "Sudah bergerak"), klik kanan menawarkan tunda 10 menit atau tutup. Prioritas bila bersamaan: peringatan BMKG berat, sholat, lalu pengingat kesehatan.
- Notifikasi yang sudah tampil sebagai notch (BMKG berat, sholat, minum, gerak) tidak diulang lagi sebagai banner di island, dan tidak lagi menahan island tetap terbuka. Notifikasi yang tiba saat island sedang terbuka (belum sempat tampil di notch) dan peringatan gempa kecil tetap memakai banner.

## [0.9.0] - belum dirilis

### Ditambahkan
- Animasi buka dan tutup jendela: Radio, Al-Qur'an, Waktu Sholat, Cuaca & Gempa, To-Do, dan Pemutar Musik keluar dari notch saat dibuka dan mengecil serta memudar ke notch (sekitar 0,3 detik) sebelum disembunyikan; membuka lagi saat sedang menutup membatalkan penutupan. Tanpa animasi saat aplikasi baru mulai atau notch belum tampil.
- Island yang terbuka karena hover kini menciut mulus ke kapsul idle (isi memudar, bingkai mengecil, sekitar 0,4 detik) alih-alih langsung berganti; bila kursor kembali ke notch saat menciut, animasi dibatalkan dan island tetap terbuka.
- Island juga membuka dengan animasi (kebalikan menciut, sekitar 0,4 detik): bingkai membesar dari kapsul idle dan isinya muncul belakangan, sehingga muncul dan hilangnya terasa setara.
- Posisi island hover mengikuti notch idle seperti notch notifikasi: bila notch digeser ke kanan layar, island melebar ke kiri dari tepi kanan kapsul idle (ke kanan dari tepi kirinya bila notch di kiri, simetris bila di tengah), dan menciutnya menuju kapsul idle yang sebenarnya, bukan ke tengah island. Animasi buka/tutup jendela juga menuju kapsul idle itu. Mengikuti pengaturan Windows "Animation effects". Jendela berisi WebView2 (YouTube Music, Spotify, Jellyfin, Browser, Live TV) belum dianimasikan.

## [0.8.1] - belum dirilis

### Diubah
- Mengklik pill jadwal sholat di island kini membuka jendela Waktu Sholat tersendiri (seperti Cuaca & Gempa), bukan memperluas island. Pill tetap ada di notch; menutup jendela hanya menyembunyikannya, dan tombol "pasang kembali ke notch" hanya muncul bila jendela memang dilepas.

### Diperbaiki
- Mengklik jadwal sholat (atau membuka panel Perluas) membuat island tampak hilang: kapsul idle berukuran tetap ikut dipakai di mode expanded sehingga seluruh isinya terpotong, dan notch kosong itu sulit diklik atau disorot. Kapsul idle kini hanya berlaku di mode compact.

## [0.8.0] - belum dirilis

### Ditambahkan
- **Pemutar Al-Qur'an** (jendela seperti Radio, ada mode mini): daftar 114 surah dengan pencarian (nama, arti, nomor, atau huruf Arab), tombol surah acak, putar/jeda, sebelumnya/berikutnya, bilah progres yang bisa diklik untuk melompat, dan volume. Pilihan setelah surah selesai: berhenti, surah berikutnya, atau acak. Tanpa teks ayat. Audio murottal Mishari Alafasy dari Quran.com API (butuh internet, tanpa cache). Dibuka dari ikon buku di island saat disorot atau menu tray.
- Saling jeda: Al-Qur'an menghentikan musik, Spotify, YouTube Music, Jellyfin, radio, dan Live TV saat mulai; pemutar tersebut atau adzan (dengan jeda otomatis) menghentikan Al-Qur'an.

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
