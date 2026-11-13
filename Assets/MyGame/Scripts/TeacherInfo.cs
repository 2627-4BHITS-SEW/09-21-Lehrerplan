
// Daten einer Lehrperson, wie sie von der HTL-Website ausgelesen werden.
// Eine reine Datenklasse: nur Variablen, keine Logik.
public class TeacherInfo
{
    public string Name;            // z.B. "Prof. Mag. Daniel Feichtinger"
    public string Room;            // z.B. "B313"
    public string PhoneExtension;  // Durchwahl ("Tel-DW"), z.B. "229". null = unbekannt
    public string DetailUrl;       // Link zur Detailseite der Person
}
