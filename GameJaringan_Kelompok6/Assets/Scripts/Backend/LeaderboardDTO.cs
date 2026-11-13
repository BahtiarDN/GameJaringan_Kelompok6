using System;
using System.Collections.Generic;
[Serializable]
public class LeaderboardEntry
{
    public string username;
    public int highScore;
}
[Serializable]
public class LeaderboardResponse
{
    public string status;
    public List<LeaderboardEntry> data;
}