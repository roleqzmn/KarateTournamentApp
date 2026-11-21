# Excel Participant Import

## Excel File Format

Use an `.xlsx` workbook. Put the participant rows on the first worksheet. The
generated template names it **Participants** and includes an **Instructions** sheet.

| Column | Header | Required | Description |
|--------|--------|----------|-------------|
| A | FirstName | Yes | Participant's first name |
| B | LastName | Yes | Participant's last name |
| C | Age | Yes | Age (integer > 0) |
| D | Club | No | Club name (optional) |
| E | Belt | Yes | Belt rank |
| F | Sex | Yes | Gender |
| G+ | Category1, Category2, etc. | Yes (min 1) | Categories (one or more columns) |

### Example:

| FirstName | LastName | Age | Club | Belt | Sex | Category1 | Category2 | Category3 |
|-----------|----------|-----|------|------|-----|-----------|-----------|-----------|
| John | Smith | 25 | ABC Club | Kyu5 | Male | Kata | Kumite | |
| Anna | Johnson | 22 | | Kyu3 | Female | Kata | Kihon | |
| Peter | Brown | 18 | XYZ Dojo | Kyu7 | Male | Kumite | Grappling | |

## Important Notes

1. **First row** must contain headers (will be skipped during import)
2. **Required fields**: FirstName, LastName, Age, Belt, Sex, and at least one Category
3. **Optional fields**: Club (can be empty)
4. Add discipline columns from G through AX. The organizer import reads past empty discipline cells. File-based mode reads consecutive discipline cells.

## Possible Values

### Belt (Rank):
- `Kyu10`, `Kyu9`, `Kyu8`, `Kyu7`, `Kyu6`, `Kyu5`, `Kyu4`, `Kyu3`, `Kyu2`, `Kyu1`, `Dan`

### Sex (Gender):
- `Male` - Male
- `Female` - Female
- `Unisex` - Mixed

### Category:
- `Kata` - Kata
- `Kumite` - Kumite
- `Kihon` - Kihon
- `KobudoShort` - Kobudo (short weapon)
- `KobudoLong` - Kobudo (long weapon)
- `Grappling` - Grappling

## Organizer workflow

1. In **Zawodnicy**, click **Wygeneruj szablon Excel** and save the workbook.
2. Enter participants from row 2 and keep the original column order.
3. Set **Podział po dodaniu lub imporcie**. The default is **Wiek i pas**.
4. Click **Importuj Excel**, choose the workbook and confirm the number of entries.

The API registers the whole batch and assigns participants to eligible
competitions or creates the missing ones. Check the new rosters in **Konkurencje**.

## Validation

- In the organizer workflow, an invalid row rejects the batch. Blank rows are skipped, and importing the same file again adds new records.
- In file-based mode, the application can skip invalid rows and duplicate first and last names. Choose the age or belt division before importing.

## Tips

- The generated template includes an "Instructions" sheet with quick reference information
- Leave cells empty for optional fields (Club) or unused category columns
- Use Excel's data validation features to prevent input errors
