namespace SLTPrivateMusicBot.Player
{
    public struct AudioMetadata
    {
        public AudioFormat format;
    }

    public struct AudioFormat
    {
        public string filename;
        public int nb_streams;
        public int nb_progress;
        public string format_name;
        public string format_long_name;
        public string start_time;
        public string duration;
        public string size;
        public string bit_rate;
        public int probe_score;
        public AudioTags tags;
    }

    public struct AudioTags
    {
        public string title;
        public string artist;
        public string track;
        public string album;
        public string album_artist;
        public string comment;
        public string date;
    }
}
