namespace autodarts_desktop.control;

// Root myDeserializedClass = JsonConvert.DeserializeObject<List<GithubTag>>(myJsonResponse);
public class Commit
{
    public string sha { get; set; }
    public string url { get; set; }
}

public class GithubTag
{
    public string? name { get; set; }
    public string zipball_url { get; set; }
    public string tarball_url { get; set; }
    public Commit commit { get; set; }
    public string node_id { get; set; }
}