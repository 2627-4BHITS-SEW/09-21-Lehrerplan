using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using UnityEngine.Networking;

/// <summary>
/// Lädt die Lehrerliste der HTL Salzburg per UnityWebRequest und liest sie mit Regex aus.
///
/// Ablauf:
///  1. Übersichtsseite laden (lehrerinnen.html) – dort stehen nur Name + Link zur Detailseite.
///     Der Raum steckt am Ende des Links, z.B. ".../feichtinger-daniel-prof-mag-b-313.html".
///  2. Für alle Lehrer:innen des gesuchten Raums die Detailseite laden und
///     Raum + Telefon-Durchwahl mit Regex auslesen.
/// </summary>
public static class HtlTeacherService
{
    public const string BaseUrl = "https://www.htl-salzburg.ac.at";
    public const string ListUrl = BaseUrl + "/lehrerinnen.html";
    public const string PhonePrefix = "+43 662 453610-";

    // Übersicht: <a class="value" href="/lehrerinnen-details/....html"><span class="text">Name</span></a>
    static readonly Regex ListEntryRegex = new Regex(
        @"<a class=""value"" href=""(?<link>/lehrerinnen-details/[^""]+)""><span class=""text"">(?<name>[^<]+)</span>",
        RegexOptions.Compiled);

    // Raum am Ende des Links: "-b-313.html"
    static readonly Regex LinkRoomRegex = new Regex(
        @"-(?<building>[a-z])-(?<number>\d{3})\.html$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Raumnummer in beliebiger Schreibweise: "B 313", "b313", "Tür B313"
    static readonly Regex RoomRegex = new Regex(
        @"(?<building>[A-Za-z])\s*(?<number>\d{3})",
        RegexOptions.Compiled);

    static string listHtml;
    static bool listLoading;
    static readonly Dictionary<string, List<TeacherInfo>> cache = new Dictionary<string, List<TeacherInfo>>();

    /// <summary>"B 313" / "Tür b313" -> "B313", sonst null.</summary>
    public static string NormalizeRoom(string text)
    {
        if (text == null) return null;
        Match m = RoomRegex.Match(text);
        return m.Success ? m.Groups["building"].Value.ToUpper() + m.Groups["number"].Value : null;
    }

    public static string FormatPhone(string extension) => PhonePrefix + extension;

    /// <summary>Coroutine: sucht alle Lehrer:innen in einem Raum.</summary>
    public static IEnumerator LoadTeachersInRoom(string room, Action<List<TeacherInfo>> onSuccess, Action<string> onError)
    {
        room = NormalizeRoom(room);
        if (room == null)
        {
            onError("Ungültige Raumnummer.");
            yield break;
        }

        if (cache.TryGetValue(room, out List<TeacherInfo> cached))
        {
            onSuccess(cached);
            yield break;
        }

        // 1. Übersichtsseite nur einmal laden (auch wenn mehrere Räume gleichzeitig angeklickt werden)
        while (listLoading) yield return null;
        if (listHtml == null)
        {
            listLoading = true;
            using (UnityWebRequest request = UnityWebRequest.Get(ListUrl))
            {
                yield return request.SendWebRequest();
                listLoading = false;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError("Lehrerliste konnte nicht geladen werden:\n" + request.error);
                    yield break;
                }
                listHtml = request.downloadHandler.text;
            }
        }

        // Kandidaten anhand des Raums im Link finden
        var candidates = new List<TeacherInfo>();
        foreach (Match m in ListEntryRegex.Matches(listHtml))
        {
            string link = m.Groups["link"].Value;
            Match linkRoom = LinkRoomRegex.Match(link);
            if (!linkRoom.Success) continue;

            string candidateRoom = linkRoom.Groups["building"].Value.ToUpper() + linkRoom.Groups["number"].Value;
            if (candidateRoom != room) continue;

            candidates.Add(new TeacherInfo
            {
                Name = WebUtility.HtmlDecode(m.Groups["name"].Value).Trim(),
                Room = candidateRoom,
                DetailUrl = BaseUrl + link
            });
        }

        // 2. Detailseiten parallel laden
        List<UnityWebRequest> requests = candidates.Select(c => UnityWebRequest.Get(c.DetailUrl)).ToList();
        foreach (UnityWebRequest request in requests) request.SendWebRequest();
        while (requests.Any(r => !r.isDone)) yield return null;

        var teachers = new List<TeacherInfo>();
        for (int i = 0; i < candidates.Count; i++)
        {
            TeacherInfo teacher = candidates[i];
            UnityWebRequest request = requests[i];

            if (request.result == UnityWebRequest.Result.Success)
            {
                string html = request.downloadHandler.text;
                string detailRoom = NormalizeRoom(ReadField(html, "Raum"));

                // Die Detailseite ist aktueller als der Link -> bei anderem Raum überspringen
                if (detailRoom != null && detailRoom != room)
                {
                    request.Dispose();
                    continue;
                }
                teacher.Name = ReadField(html, "Lehrername") ?? teacher.Name;
                teacher.PhoneExtension = ReadField(html, "Telefon");
            }
            teachers.Add(teacher);
            request.Dispose();
        }

        cache[room] = teachers;
        onSuccess(teachers);
    }

    /// <summary>
    /// Liest ein Feld der Detailseite aus, z.B.
    /// &lt;div class="field Telefon"&gt;&lt;div class="label"&gt;Tel-DW:&lt;/div&gt;&lt;div class="value"&gt; &lt;span class="text"&gt;229&lt;/span&gt;
    /// </summary>
    static string ReadField(string html, string field)
    {
        Match m = Regex.Match(html,
            @"<div class=""field " + field + @""">\s*<div class=""label"">[^<]*</div>\s*<div class=""value"">\s*<span class=""text"">(?<value>[^<]*)</span>");
        if (!m.Success) return null;

        string value = WebUtility.HtmlDecode(m.Groups["value"].Value).Trim();
        return value == "" || value == "-" ? null : value;
    }
}
