using System;

[Serializable]
public class JSONResponse
{
    [Serializable]
    public class Part
    {
        public string text;
    }

    [Serializable]
    public class Content
    {
        public Part[] parts;
    }

    [Serializable]
    public class Candidate
    {
        public Content content;
    }

    public Candidate[] candidates;
}