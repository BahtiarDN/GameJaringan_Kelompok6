using System;

/// <summary>
/// DTO (Data Transfer Object) untuk komunikasi dengan GameBackend (Express + SQLite).
/// Field harus cocok dengan server.js:
/// - POST /api/score/save            -> body {userId, score}
/// - GET  /api/score/highscore/:id   -> response {status, userId, highScore}
/// </summary>

[Serializable]
public class ScoreSaveRequest
{
    public int userId;
    public int score;
}

[Serializable]
public class ScoreSaveResponse
{
    public string status;
    public string message;
    public int scoreId;
}

[Serializable]
public class HighScoreResponse
{
    public string status;
    public int userId;
    public int highScore;
}
