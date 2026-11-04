require('dotenv').config();
const express = require('express');
const cors = require('cors');

const app = express();
const PORT = process.env.PORT || 5000;

// Middleware
app.use(cors());
app.use(express.json());

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
  const sql = `SELECT MAX(score) AS highScore FROM scores WHERE user_id = ?`;
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

app.listen(PORT, () => {
  console.log('=================================');
  console.log(' Server Game Backend Berjalan!');
  console.log(` URL: http://localhost:${PORT}`);
  console.log('=================================');
});