const sqlite3 = require('sqlite3').verbose();

const db = new sqlite3.Database('./game_data.db', (err) => {
    if (err) {
        console.error('Error database:', err.message);
    } else {
        console.log('Terhubung ke database SQLite.');
    }
});

// Inisialisasi Tabel Users & Scores
db.serialize(() => {
        db.run(`CREATE TABLE IF NOT EXISTS users (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        username TEXT UNIQUE NOT NULL,
        password_hash TEXT NOT NULL,
        created_at DATETIME DEFAULT CURRENT_TIMESTAMP
    )`);

        db.run(`CREATE TABLE IF NOT EXISTS scores (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        user_id INTEGER NOT NULL,
        score INTEGER NOT NULL,
        played_at DATETIME DEFAULT CURRENT_TIMESTAMP,
        FOREIGN KEY (user_id) REFERENCES users(id)
    )`, (err) => {
        if (!err) console.log('Tabel scores siap digunakan.');
    });
});

module.exports = db;