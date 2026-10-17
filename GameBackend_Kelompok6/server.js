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
app.post('/api/score/save', (req, res) => {
  res.status(501).json({ message: 'Endpoint /api/score/save belum diimplementasikan' });
});
app.get('/api/leaderboard', (req, res) => {
  res.status(501).json({ message: 'Endpoint /api/leaderboard belum diimplementasikan' });
});

app.listen(PORT, () => {
  console.log('=================================');
  console.log(' Server Game Backend Berjalan!');
  console.log(` URL: http://localhost:${PORT}`);
  console.log('=================================');
});