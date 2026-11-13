using System;
using System.Collections.Generic;
[Serializable]
public class LeaderboardEntry
{
    public int userId;
    public int score;
}
[Serializable]
public class LeaderboardResponse
{
    public string status;
    public List<LeaderboardEntry> data;
}