| Column Name            | Data Type | Fixed Width | Start Column | Notes / Range                                                                                  |
|------------------------|-----------|-------------|--------------|-------------------------------------------------------------------------------------------------|
| Title                  | string    | 60          | 1            | Free text; observed lengths ≈ 20–60; truncate if >60, pad with spaces to align.               |
| Author                 | string    | 25          | 61           | Author name; observed lengths ≈ 12–25; padded to 25.                                           |
| Publication Year       | int       | 4           | 86           | 4-digit year; observed range 2018–2024.                                                         |
| Pages                  | int       | 4           | 90           | Positive integer; observed range 241–355; padded to 4.                                         |
| Publisher              | string    | 30          | 94           | Publisher name; observed lengths ≈ 12–35; truncate/pad to 30.                                   |
| Setting (City/State)   | string    | 25          | 124          | City + state (e.g., "Sault Ste. Marie, MI"); pad to 25 to accommodate longer names.            |
| Protagonists           | string    | 30          | 149          | Short paired-role descriptions; observed lengths ≈ 10–30; pad to 30.                           |
| Romance Tropes         | string    | 30          | 179          | Tag list or short phrase; examples: "Slow burn; winter romance"; pad to 30.                    |
| One Liner              | string    | 100         | 209          | Single-sentence synopsis; observed lengths ≈ 40–140; truncate to 100 for fixed-width export.    |