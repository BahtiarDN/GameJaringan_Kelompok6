require('dotenv').config();
const express = require('express');
const cors = require('cors');

const app = express();
const PORT = process.env.PORT || 5000;

// Middleware
app.use(cors());
app.use(express.json());

// ============================================================
// KONEKSI DATABASE - LAZY & AMAN (REVISI)
// ============================================================
// STRUKTUR DIBALIKIN SEPENUH AWAL: TIDAK ADA require('./database')
// di bagian atas file, jadi perilaku start server SAMA PERSIS dengan
// semula - server PASTI start walau modul sqlite3 belum di-install
// di komputer teman satu tim (mis. baru git pull).
//
// Fungsinya tetap ada & tetap hidup, tapi require database dilakukan
// DI DALAM route-nya saja, dibungkus try/catch:
// - Jika sqlite3 ditemukan  -> endpoint skor bekerja normal.
// - Jika sqlite3 tidak ada  -> server TIDAK crash, hanya membalas
//   pesan "Jalankan: npm install sqlite3" (500). Health check lain damai.
let dbInstance = null;
function getDb() {
  if (dbInstance === null) {
    dbInstance = require('./database'); // koneksi SQLite (lazily, pertama dipakai)
  }
  return dbInstance;
}

// Health check
app.get('/api/health', (req, res) => {
  res.status(200).json({
    status: 'success',
    message: 'Game Backend Server is running successfully!',
    serverTime: new Date().toISOString()
  });
});

// Draft endpoint (diimplementasikan di pertemuan berikutnya)
app.post('/api/register', (req, res) => {
  res.status(501).json({ message: 'Endpoint /api/register belum diimplementasikan' });
});

app.post('/api/login', (req, res) => {
  res.status(501).json({ message: 'Endpoint /api/login belum diimplementasikan' });
});

// --- ENDPOINT /api/score/save: SIMPAN SKOR PERTANDINGAN ---
app.post('/api/score/save', (req, res) => {
  const { userId, score } = req.body;

  if (userId === undefined || score === undefined) {
  return res.status(400).json({ status: 'error', message: 'userId dan score wajib diisi!' });
  }

  let db;
  try {
    db = getDb();
  } catch (err) {
    console.error('Modul sqlite3 tidak tersedia:', err.message);
    return res.status(500).json({
      status: 'error',
      message: 'Database belum siap. Jalankan: npm install sqlite3 (di folder GameBackend_Kelompok6).'
    });
  }

  const sql = `INSERT INTO scores (user_id, score) VALUES (?, ?)`;
  db.run(sql, [userId, score], function(err) {
    if (err) {
      console.error('Gagal menyimpan skor:', err.message);
      return res.status(500).json({ status: 'error', message: 'Gagal menyimpan skor ke database.' });
    }

    console.log(`[SCORE SAVED] UserID: ${userId}, Score: ${score}`);
    res.status(201).json({
      status: 'success',
      message: 'Skor berhasil disimpan!',
      scoreId: this.lastID
    });
  });
});

// --- ENDPOINT /api/score/highscore/:userId: AMBIL SKOR TERTINGGI PEMAIN (PERSONAL HIGH SCORE) ---
app.get('/api/score/highscore/:userId', (req, res) => {
  const userId = req.params.userId;
  let db;
  try {
    db = getDb();
  } catch (err) {
    console.error('Modul sqlite3 tidak tersedia:', err.message);
    return res.status(500).json({
      status: 'error',
      message: 'Database belum siap. Jalankan: npm install sqlite3 (di folder GameBackend_Kelompok6).'
    });
  }

  const sql = `
    SELECT MAX(score) AS highScore 
    FROM scores 
    WHERE user_id = ?
  `;
  db.get(sql, [userId], (err, row) => {
    if (err) {
    console.error('Gagal mengambil high score:', err.message);
      return res.status(500).json({ status: 'error', message: 'Gagal mengambil data dari database.' });
    }

    // Jika pemain belum pernah memiliki skor, kembalikan 0
    const personalHighScore = (row && row.highScore !== null) ? row.highScore : 0;
    console.log(`[HIGH SCORE FETCH] UserID: ${userId}, HighScore: ${personalHighScore}`);
    res.status(200).json({
      status: 'success',
      userId: parseInt(userId),
      highScore: personalHighScore
    });
  });
});

// --- ENDPOINT GET LEADERBOARD (TOP 10 HIGH SCORES) ---
app.get('/api/leaderboard', (req, res) => {
  let db;
  try {
    db = getDb();
  } catch (err) {
    console.error('Modul sqlite3 tidak tersedia:', err.message);
    return res.status(500).json({
      status: 'error',
      message: 'Database belum siap. Jalankan: npm install sqlite3 (di folder GameBackend_Kelompok6).'
    });
  }

  const sql = `
    SELECT user_id AS userId, score
    FROM scores
    ORDER BY score DESC, played_at DESC, id DESC
  `;

  db.all(sql, [], (err, rows) => {
    if (err) {
      console.error('Gagal mengambil leaderboard:', err.message);
      return res.status(500).json({ status: 'error', message: 'Gagal mengambil data leaderboard.' });
    }

    res.status(200).json({
      status: 'success',
      data: rows
    });
  });
});

app.listen(PORT, () => {
  console.log('=================================');
  console.log(' Server Game Backend Berjalan!');
  console.log(` URL: http://localhost:${PORT}`);
  console.log('=================================');
});
