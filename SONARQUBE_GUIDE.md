# 🛡️ Panduan Lengkap SonarQube & Sonar Ecosystem (Clean Code Guide)

> **Proyek:** EDCL GPS Telematics API  
> **Target:** Panduan Pemahaman & Operasional Kualitas Kode untuk Developer & Tim Engineering  
> **Teknologi:** SonarQube Community Edition · .NET 10 · Docker Compose · Coverlet OpenCover  

---

## 1. Apa itu SonarQube (dan Keluarga "Sonar / SonarCode")?

**SonarQube** (sering disebut oleh tim developer sebagai *Sonar*, *SonarCode*, atau *SonarSource*) adalah platform **Static Application Security Testing (SAST)** dan **Code Quality Analysis** terpopuler di industri perangkat lunak. 

SonarQube bertindak sebagai **"Inspektur Otomatis"** yang memindai (*scan*) seluruh baris kode sumber tanpa harus mengeksekusi aplikasi secara langsung. Tujuannya adalah mendeteksi kelemahan keamanan, potensi *bug*, kode duplikat, dan *bad practices* sedini mungkin di komputer lokal atau pipeline CI/CD sebelum kode sampai ke lingkungan *production*.

### Ekosistem Produk Sonar:
Untuk menghindari kerancuan istilah:
1. **SonarQube (Server):** Dashboard web terpusat dan mesin penganalisis utama (inilah yang kita pasang di Docker pada `http://localhost:9000`).
2. **SonarScanner (`dotnet-sonarscanner`):** Agen/alat CLI pengeksekusi analisis di mesin pengembang atau server CI/CD yang mengompilasi kode, mengumpulkan hasil pengujian, lalu mengunggah laporannya ke SonarQube Server.
3. **SonarLint:** Ekstensi IDE (tersedia untuk VS Code, Visual Studio, JetBrains Rider) yang bekerja seperti *spell-checker*, mendeteksi *code smell* secara *real-time* saat developer sedang mengetik kode.
4. **SonarCloud:** Versi cloud berbasis SaaS (dikelola oleh SonarSource) dari SonarQube.

---

## 2. Empat Pilar Utama yang Diperiksa oleh SonarQube

SonarQube mengelompokkan temuan kode ke dalam 4 pilar kualitas (**Clean Code Taxonomy**):

```
┌────────────────────────────────────────────────────────────────────────┐
│                        4 PILAR ANALISIS SONARQUBE                      │
├─────────────────────┬───────────────────┬──────────────────────────────┤
│ Pilar               │ Kategori Masalah  │ Pertanyaan Kunci             │
├─────────────────────┼───────────────────┼──────────────────────────────┤
│ 1. Security         │ Vulnerabilities & │ "Apakah kode ini mudah       │
│                     │ Security Hotspots │  diretas atau bocor datanya?"│
├─────────────────────┼───────────────────┼──────────────────────────────┤
│ 2. Reliability      │ Bugs              │ "Apakah kode ini akan crash  │
│                     │                   │  atau melempar exception?"   │
├─────────────────────┼───────────────────┼──────────────────────────────┤
│ 3. Maintainability  │ Code Smells &     │ "Apakah kode ini rumit, kotor│
│                     │ Technical Debt    │  dan susah dirawat?"         │
├─────────────────────┼───────────────────┼──────────────────────────────┤
│ 4. Coverage & CPD   │ Test Coverage &   │ "Berapa persen kode yang     │
│                     │ Duplications      │  diuji & seberapa duplikat?" │
└─────────────────────┴───────────────────┴──────────────────────────────┘
```

---

### A. 🛡️ Security (Keamanan)
Mendeteksi kerentanan yang berisiko dieksploitasi oleh pihak luar:
* **Vulnerabilities (Kerentanan Pasti):** Kelemahan nyata yang harus segera diperbaiki.
  * *Contoh:* SQL Injection (`string concatenation` pada query DB), mengekspos password di response JSON API, penggunaan algoritma hashing yang usang (MD5, SHA1).
* **Security Hotspots (Titik Rawan Keamanan):** Bagian kode yang peka terhadap keamanan dan memerlukan peninjauan (*manual review*) oleh developer untuk memastikan konteks penggunaannya aman.
  * *Contoh:* Kebijakan CORS `AllowAnyOrigin()` (CORS terlalu terbuka), ekspresi reguler (Regex) yang rentan terhadap serangan ReDoS (*Regular Expression Denial of Service*).

---

### B. 🪲 Reliability (Keandalan / Bugs)
Mendeteksi kesalahan logika pemrograman yang berpotensi memicu kegagalan runtime (*runtime crash*):
* **Null Pointer / Reference Risk:** Memanggil properti dari objek yang berpotensi `null` tanpa pengecekan.
* **Async/Await Pitfalls:** Menggunakan `.Result` atau `.Wait()` yang dapat memicu *threadpool starvation* / *deadlock*, atau *fire-and-forget task* tanpa penanganan *exception*.
* **Resource Leaks:** Lupa menutup koneksi database, file stream, atau channel pesan (`IDisposable` tidak di-`dispose` secara benar).
* **Empty Catch Blocks:** Menelan *exception* secara diam-diam (`catch { }`) sehingga error di production tidak terlacak sama sekali.

---

### C. 🧹 Maintainability (Keterawatan / Code Smells)
Mendeteksi kode yang membingungkan, boros memori, atau sulit dipahami oleh developer lain:
* **Cognitive Complexity (Kompleksitas Kognitif):** Mengukur seberapa sulit alur kode dipahami oleh otak manusia. Jika sebuah method memiliki terlalu banyak `if/else`, `switch`, dan *nested loop* (ambang batas normal SonarQube adalah skor **15**), SonarQube akan meminta method tersebut dipecah (*refactor*).
* **Dead Code:** Variabel, method, atau file kelas yang dideklarasikan tetapi tidak pernah dipakai di mana pun.
* **Technical Debt (Hutang Teknis):** SonarQube menghitung perkiraan **waktu (misal: 2h 30min)** yang dibutuhkan untuk merapikan kode tersebut.

---

### D. 🧪 Coverage & Duplikasi
* **Unit Test Coverage:** Persentase baris kode dan percabangan (*branch coverage*) yang dieksekusi oleh tes unit. Standar industri biasanya menargetkan minimal **80% coverage pada kode baru**.
* **Code Duplication (CPD):** Potongan logika kode yang di-copy-paste di beberapa file berbeda. Duplikasi tinggi menandakan perlunya abstraksi atau *shared utility*.

---

## 3. Tingkat Keparahan (Severity) & Rating SonarQube

Setiap temuan diberi label tingkat keparahan:

| Severity | Definisi | Tindakan Wajib |
| :--- | :--- | :--- |
| **🔴 Blocker** | Bug berisiko tinggi merusak aplikasi atau celah keamanan kritis di production. | Wajib diperbaiki segera. Pipeline build gagal. |
| **🟠 Critical** | Kerentanan keamanan signifikan atau bug logika berat. | Wajib diperbaiki sebelum rilis. |
| **🟡 Major** | Kualitas kode buruk yang berdampak besar pada produktivitas tim (misal: method 300+ baris). | Direkomendasikan diperbaiki dalam sprint ini. |
| **🔵 Minor** | Masalah kualitas ringan (misal: penamaan variabel tidak sesuai konvensi C#). | Diperbaiki bila ada waktu. |
| **⚪ Info** | Catatan informatif atau saran gaya penulisan (*style preference*). | Opsional. |

### Skor Penilaian (Rating A s/d E):
SonarQube memberikan nilai rapor (*grade*) untuk setiap aspek:
* **A (Terbaik):** 0 masalah Blocker/Critical, < 5% hutang teknis.
* **B:** Kerentanan ringan atau hutang teknis 6–10%.
* **C s/d E:** Banyak kerentanan, bug aktif, atau kode sangat berantakan.

---

## 4. Konsep "Clean as You Code" (CaYC) & Quality Gate

Banyak tim gagal mengadopsi SonarQube karena mereka frustrasi melihat ribuan *warning* pada kode lama (*legacy code*).

SonarQube memecahkan masalah ini dengan filosofi **Clean as You Code (CaYC)**:
> *"Jangan khawatirkan seluruh kode masa lalu sekaligus. Cukup pastikan setiap baris kode baru (New Code) yang ditulis hari ini bersih dan lulus standar!"*

### Apa itu Quality Gate?
**Quality Gate** adalah "palang pintu otomatis" yang menentukan apakah suatu *Pull Request* atau *commit* layak digabungkan (*merge*).

Kriteria standar Quality Gate pada **New Code**:
1. **0 Vulnerabilities Baru** (Keamanan 100% aman)
2. **0 Bugs Baru** (Tidak ada potensi crash baru)
3. **Rating Keamanan & Keandalan: Grade A**
4. **Coverage Pengujian Baru: Minimal 80%**
5. **Duplikasi Kode Baru: Maksimal 3%**

---

## 5. Panduan Penggunaan di Proyek EDCL GPS API

Sistem ini telah dilengkapi dengan arsitektur SonarQube berbasis Docker dan otomatisasi Makefile:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        WORKFLOW HARIAN PENGEMBANG                      │
├────────────────────────────────────────────────────────────────────────┤
│ 1. Jalankan SonarQube Server : make sonar-up                           │
│ 2. Cek Status                : make sonar-status                       │
│ 3. Jalankan Analisis Lengkap : make sonar-scan token=<TOKEN>           │
│ 4. Lihat Hasil di Browser    : http://localhost:9000                   │
│ 5. Matikan Setelah Selesai   : make sonar-down                         │
└────────────────────────────────────────────────────────────────────────┘
```

### Penjelasan Perintah Makefile:

| Perintah | Fungsi |
| :--- | :--- |
| `make sonar-up` | Menyalakan container `edcl-sonarqube-server` dan `edcl-sonarqube-db` di Docker secara terisolasi. Otomatis memvalidasi limit kernel Linux (`vm.max_map_count`). |
| `make sonar-status` | Mengecek kesehatan container dan endpoint API SonarQube. |
| `make sonar-logs` | Melihat live log container SonarQube (berguna saat booting pertama kali). |
| `make sonar-scan token=<TOKEN>` | Menjalankan proses 4-tahap: `sonarscanner begin` ➡️ `dotnet build` ➡️ `dotnet test` (OpenCover coverage) ➡️ `sonarscanner end` yang mengirim laporan ke dashboard. |
| `make sonar-down` | Menghentikan container SonarQube tanpa menghapus riwayat data scan. |
| `make sonar-clean` | Menghapus container beserta volume data (gunakan bila ingin reset total). |

---

## 6. Cara Mendapatkan Token Pertama Kali

1. Buka browser: **[http://localhost:9000](http://localhost:9000)**
2. Login awal:
   * **Username:** `admin`
   * **Password:** `admin`
3. Masukkan password baru yang kuat.
4. Klik avatar Anda di kanan atas ➡️ **My Account** ➡️ **Security**.
5. Di kolom **Generate Token**:
   * Nama Token: `edcl-local-token`
   * Type: `User Token`
   * Expiration: Bebas (misal: 30 days atau No Expiration untuk lokal)
   * Klik **Generate**.
6. Simpan token tersebut (misal `squ_xxxxxxxxxxxxxxxxxxxx`).
7. Anda dapat menyimpannya di terminal agar tidak perlu mengetik token berulang kali:
   ```bash
   export SONAR_TOKEN="squ_xxxxxxxxxxxxxxxxxxxx"
   make sonar-scan
   ```

---

## 7. Tips Menjaga Kode Bersih (.NET 10 Best Practices)

Saat menulis fitur baru pada layanan **VendorMaster**, **DeliveryTracking**, atau **GpsCollector**:
1. **Gunakan Nullable Reference Types:** Tandai tipe data yang bisa null dengan `?` (`string?`, `ILogger?`) dan selalu periksa sebelum memanggil propertinya.
2. **Hindari Hardcoded Secrets:** Jangan pernah menaruh password DB atau token vendor di dalam kode C#. Gunakan `IConfiguration` atau environment variables.
3. **Pecah Method Panjang:** Usahakan satu method tidak melebihi 40–50 baris kode dan memiliki tanggung jawab tunggal (*Single Responsibility Principle*).
4. **Tulis Unit Test:** Setiap membuat *Use Case* / *Command Handler* baru di folder `Application/UseCases/`, tambahkan unit test pendukungnya agar metrik *Coverage* tetap hijau di SonarQube!
