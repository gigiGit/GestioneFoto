$script:estrattore = Join-Path $PSScriptRoot 'estraiArchivio.ps1'

if (-not ('EstraiArchivioTestLink' -as [type])) {
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class EstraiArchivioTestLink
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool CreateSymbolicLink(string link, string target, int flags);
}
'@
}

function Write-TarField {
    param([byte[]]$Buffer, [int]$Offset, [int]$Length, [string]$Value)

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
    if ($bytes.Length -gt $Length) {
        throw "Campo TAR troppo lungo: $Value"
    }
    [Array]::Copy($bytes, 0, $Buffer, $Offset, $bytes.Length)
}

function Write-TarOctal {
    param([byte[]]$Buffer, [int]$Offset, [int]$Length, [long]$Value)

    $testo = [Convert]::ToString($Value, 8).PadLeft($Length - 1, '0')
    Write-TarField $Buffer $Offset ($Length - 1) $testo
}

function New-TestArchive {
    param([string]$Path, [object[]]$Entries)

    $tar = [System.IO.MemoryStream]::new()
    try {
        foreach ($entry in $Entries) {
            $tipo = '0'
            if ($entry.ContainsKey('Type')) {
                $tipo = $entry.Type
            }

            [byte[]]$contenuto = @()
            if ($tipo -eq '0') {
                $contenuto = [System.Text.Encoding]::UTF8.GetBytes([string]$entry.Content)
            }

            $header = New-Object byte[] 512
            Write-TarField $header 0 100 $entry.Name
            Write-TarOctal $header 100 8 420
            Write-TarOctal $header 108 8 0
            Write-TarOctal $header 116 8 0
            Write-TarOctal $header 124 12 $contenuto.Length
            Write-TarOctal $header 136 12 1700000000
            for ($i = 148; $i -lt 156; $i++) {
                $header[$i] = 32
            }
            $header[156] = [byte][char]$tipo
            if ($entry.ContainsKey('LinkName')) {
                Write-TarField $header 157 100 $entry.LinkName
            }
            Write-TarField $header 257 6 'ustar'
            Write-TarField $header 263 2 '00'

            [long]$checksum = 0
            foreach ($byte in $header) {
                $checksum += $byte
            }
            Write-TarField $header 148 6 ([Convert]::ToString($checksum, 8).PadLeft(6, '0'))
            $header[154] = 0
            $header[155] = 32

            $tar.Write($header, 0, $header.Length)
            if ($contenuto.Length -gt 0) {
                $tar.Write($contenuto, 0, $contenuto.Length)
                $resto = $contenuto.Length % 512
                if ($resto -ne 0) {
                    $padding = New-Object byte[] (512 - $resto)
                    $tar.Write($padding, 0, $padding.Length)
                }
            }
        }

        $fine = New-Object byte[] 1024
        $tar.Write($fine, 0, $fine.Length)
        $tar.Position = 0
        $file = [System.IO.File]::Create($Path)
        try {
            $gzip = [System.IO.Compression.GZipStream]::new(
                $file, [System.IO.Compression.CompressionMode]::Compress
            )
            try {
                $tar.CopyTo($gzip)
            } finally {
                $gzip.Dispose()
            }
        } finally {
            $file.Dispose()
        }
    } finally {
        $tar.Dispose()
    }
}

Describe 'estraiArchivio.ps1' {
    $probeTarget = Join-Path $TestDrive 'probe-target'
    $probeLink = Join-Path $TestDrive 'probe-link'
    [System.IO.File]::WriteAllText($probeTarget, 'test')
    try {
        $script:linkDisponibili = [EstraiArchivioTestLink]::CreateSymbolicLink($probeLink, $probeTarget, 2)
        if (-not $script:linkDisponibili) {
            $script:linkDisponibili = [EstraiArchivioTestLink]::CreateSymbolicLink($probeLink, $probeTarget, 0)
        }
    } finally {
        if ($script:linkDisponibili) {
            [System.IO.File]::Delete($probeLink)
        }
        [System.IO.File]::Delete($probeTarget)
    }

    BeforeEach {
        $script:cartellaTest = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:cartellaTest | Out-Null
    }

    It 'estrae un jolly, mantiene le cartelle e conserva tutte le versioni duplicate' {
        $archivi = Join-Path $script:cartellaTest 'archivi con spazi'
        $destinazione = Join-Path $script:cartellaTest 'foto [output]'
        New-Item -ItemType Directory -Path $archivi | Out-Null
        New-TestArchive (Join-Path $archivi 'b.tgz') @(
            @{ Name = 'album/foto.txt'; Content = 'terza' },
            @{ Name = 'album/altra.txt'; Content = 'duplicata' }
        )
        New-TestArchive (Join-Path $archivi 'a.tgz') @(
            @{ Name = 'album/foto.txt'; Content = 'prima' },
            @{ Name = 'album/foto.txt'; Content = 'seconda' },
            @{ Name = 'album/altra.txt'; Content = 'originale' },
            @{ Name = 'vuota/'; Type = '5' },
            @{ Name = 'zero.txt'; Content = '' }
        )

        Push-Location -LiteralPath $archivi
        try {
            & $script:estrattore '*.tgz' -d $destinazione
        } finally {
            Pop-Location
        }

        [System.IO.File]::ReadAllText((Join-Path $destinazione 'album\foto.txt')) | Should Be 'prima'
        [System.IO.File]::ReadAllText((Join-Path $destinazione 'album\duplicati-1\foto.txt')) | Should Be 'seconda'
        [System.IO.File]::ReadAllText((Join-Path $destinazione 'album\duplicati-2\foto.txt')) | Should Be 'terza'
        [System.IO.File]::ReadAllText((Join-Path $destinazione 'album\duplicati-1\altra.txt')) | Should Be 'duplicata'
        (Test-Path -LiteralPath (Join-Path $destinazione 'vuota') -PathType Container) | Should Be $true
        (Get-Item -LiteralPath (Join-Path $destinazione 'zero.txt')).Length | Should Be 0
    }

    It 'non sovrascrive i file presenti e salta le sottocartelle occupate da file' {
        $destinazione = Join-Path $script:cartellaTest 'output'
        $album = Join-Path $destinazione 'album'
        New-Item -ItemType Directory -Path (Join-Path $album 'duplicati-1') -Force | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $album 'foto.txt'), 'esistente')
        [System.IO.File]::WriteAllText((Join-Path $album 'duplicati-1\foto.txt'), 'precedente')
        [System.IO.File]::WriteAllText((Join-Path $album 'duplicati-2'), 'occupato')
        $archivio = Join-Path $script:cartellaTest 'foto.tgz'
        New-TestArchive $archivio @(@{ Name = 'album/foto.txt'; Content = 'nuova' })

        & $script:estrattore -d $destinazione $archivio

        [System.IO.File]::ReadAllText((Join-Path $album 'foto.txt')) | Should Be 'esistente'
        [System.IO.File]::ReadAllText((Join-Path $album 'duplicati-1\foto.txt')) | Should Be 'precedente'
        [System.IO.File]::ReadAllText((Join-Path $album 'duplicati-2')) | Should Be 'occupato'
        [System.IO.File]::ReadAllText((Join-Path $album 'duplicati-3\foto.txt')) | Should Be 'nuova'
    }

    It 'accetta piu percorsi e non elabora due volte lo stesso archivio' {
        $primo = Join-Path $script:cartellaTest 'primo.tgz'
        $secondo = Join-Path $script:cartellaTest 'secondo.tgz'
        $destinazione = Join-Path $script:cartellaTest 'output'
        New-TestArchive $primo @(@{ Name = 'foto.txt'; Content = 'prima' })
        New-TestArchive $secondo @(@{ Name = 'foto.txt'; Content = 'seconda' })

        & $script:estrattore $primo $secondo $primo -d $destinazione

        [System.IO.File]::ReadAllText((Join-Path $destinazione 'foto.txt')) | Should Be 'prima'
        [System.IO.File]::ReadAllText((Join-Path $destinazione 'duplicati-1\foto.txt')) | Should Be 'seconda'
        (Test-Path -LiteralPath (Join-Path $destinazione 'duplicati-2\foto.txt')) | Should Be $false
    }

    It 'estrae un archivio vuoto senza usare un global.json della cartella corrente' {
        $archivio = Join-Path $script:cartellaTest 'vuoto.tgz'
        $destinazione = Join-Path $script:cartellaTest 'output'
        $lavoro = Join-Path $script:cartellaTest 'lavoro'
        New-Item -ItemType Directory -Path $lavoro | Out-Null
        [System.IO.File]::WriteAllText(
            (Join-Path $lavoro 'global.json'), '{"sdk":{"version":"1.0.0","rollForward":"disable"}}'
        )
        New-TestArchive $archivio @()

        Push-Location -LiteralPath $lavoro
        try {
            & $script:estrattore $archivio -d $destinazione
            (Get-Location).Path | Should Be $lavoro
        } finally {
            Pop-Location
        }

        (Test-Path -LiteralPath $destinazione -PathType Container) | Should Be $true
        @(Get-ChildItem -LiteralPath $destinazione -Force).Count | Should Be 0
    }

    It 'estrae i collegamenti rigidi come copie, mantenendo i duplicati' {
        $archivio = Join-Path $script:cartellaTest 'collegamenti.tgz'
        $destinazione = Join-Path $script:cartellaTest 'output'
        New-TestArchive $archivio @(
            @{ Name = 'anticipata.txt'; Type = '1'; LinkName = 'originale.txt' },
            @{ Name = 'originale.txt'; Content = 'contenuto' },
            @{ Name = 'copia.txt'; Type = '1'; LinkName = 'originale.txt' },
            @{ Name = 'copia.txt'; Type = '1'; LinkName = 'originale.txt' },
            @{ Name = 'originale.txt'; Type = '1'; LinkName = 'originale.txt' }
        )

        & $script:estrattore $archivio -d $destinazione

        [System.IO.File]::ReadAllText((Join-Path $destinazione 'anticipata.txt')) | Should Be 'contenuto'
        [System.IO.File]::ReadAllText((Join-Path $destinazione 'copia.txt')) | Should Be 'contenuto'
        [System.IO.File]::ReadAllText((Join-Path $destinazione 'duplicati-1\copia.txt')) | Should Be 'contenuto'
        [System.IO.File]::ReadAllText((Join-Path $destinazione 'duplicati-1\originale.txt')) | Should Be 'contenuto'
    }

    It 'non segue junction preesistenti nelle cartelle di destinazione' {
        $archivio = Join-Path $script:cartellaTest 'foto.tgz'
        $esterno = Join-Path $script:cartellaTest 'esterno'
        $destinazione = Join-Path $script:cartellaTest 'output'
        $album = Join-Path $destinazione 'album'
        $junction = Join-Path $album 'duplicati-1'
        New-Item -ItemType Directory -Path $esterno, $album -Force | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $album 'foto.txt'), 'esistente')
        New-TestArchive $archivio @(@{ Name = 'album/foto.txt'; Content = 'nuova' })
        New-Item -ItemType Junction -Path $junction -Target $esterno | Out-Null

        try {
            & $script:estrattore $archivio -d $destinazione

            [System.IO.File]::ReadAllText((Join-Path $album 'duplicati-2\foto.txt')) | Should Be 'nuova'
            (Test-Path -LiteralPath (Join-Path $esterno 'foto.txt')) | Should Be $false
        } finally {
            [System.IO.Directory]::Delete($junction)
        }
    }

    It 'rifiuta una cartella interna che punta fuori dalla destinazione' {
        $archivio = Join-Path $script:cartellaTest 'foto.tgz'
        $esterno = Join-Path $script:cartellaTest 'esterno'
        $destinazione = Join-Path $script:cartellaTest 'output'
        $junction = Join-Path $destinazione 'album'
        New-Item -ItemType Directory -Path $esterno, $destinazione -Force | Out-Null
        New-TestArchive $archivio @(@{ Name = 'album/foto.txt'; Content = 'nuova' })
        New-Item -ItemType Junction -Path $junction -Target $esterno | Out-Null

        try {
            { & $script:estrattore $archivio -d $destinazione } | Should Throw
            (Test-Path -LiteralPath (Join-Path $esterno 'foto.txt')) | Should Be $false
        } finally {
            [System.IO.Directory]::Delete($junction)
        }
    }

    It 'segnala un jolly senza corrispondenze senza creare la destinazione' {
        $destinazione = Join-Path $script:cartellaTest 'output'
        $pattern = Join-Path $script:cartellaTest 'nessuno-*.tgz'

        { & $script:estrattore $pattern -d $destinazione } | Should Throw
        (Test-Path -LiteralPath $destinazione) | Should Be $false
    }

    It 'controlla tutti i percorsi prima di creare la destinazione' {
        $archivio = Join-Path $script:cartellaTest 'valido.tgz'
        $mancante = Join-Path $script:cartellaTest 'mancante-*.tgz'
        $destinazione = Join-Path $script:cartellaTest 'output'
        New-TestArchive $archivio @(@{ Name = 'foto.txt'; Content = 'foto' })

        { & $script:estrattore $archivio $mancante -d $destinazione } | Should Throw
        (Test-Path -LiteralPath $destinazione) | Should Be $false
    }

    It 'segnala un archivio non gzip senza alterare file esistenti' {
        $archivio = Join-Path $script:cartellaTest 'errato.tgz'
        $destinazione = Join-Path $script:cartellaTest 'output'
        New-Item -ItemType Directory -Path $destinazione | Out-Null
        [System.IO.File]::WriteAllText($archivio, 'non e gzip')
        [System.IO.File]::WriteAllText((Join-Path $destinazione 'esistente.txt'), 'intatto')

        { & $script:estrattore $archivio -d $destinazione } | Should Throw
        [System.IO.File]::ReadAllText((Join-Path $destinazione 'esistente.txt')) | Should Be 'intatto'
    }

    It 'segnala un checksum gzip errato' {
        $archivio = Join-Path $script:cartellaTest 'checksum.tgz'
        $destinazione = Join-Path $script:cartellaTest 'output'
        New-TestArchive $archivio @(@{ Name = 'foto.txt'; Content = 'contenuto' })
        [byte[]]$bytes = [System.IO.File]::ReadAllBytes($archivio)
        $bytes[$bytes.Length - 8] = $bytes[$bytes.Length - 8] -bxor 1
        [System.IO.File]::WriteAllBytes($archivio, $bytes)

        { & $script:estrattore $archivio -d $destinazione } | Should Throw
    }

    It 'segnala un archivio gzip senza il trailer' {
        $archivio = Join-Path $script:cartellaTest 'troncato.tgz'
        $destinazione = Join-Path $script:cartellaTest 'output'
        New-TestArchive $archivio @(@{ Name = 'foto.txt'; Content = 'contenuto' })
        [byte[]]$completo = [System.IO.File]::ReadAllBytes($archivio)
        $troncato = New-Object byte[] ($completo.Length - 8)
        [Array]::Copy($completo, $troncato, $troncato.Length)
        [System.IO.File]::WriteAllBytes($archivio, $troncato)

        { & $script:estrattore $archivio -d $destinazione } | Should Throw
        (Test-Path -LiteralPath (Join-Path $destinazione 'foto.txt')) | Should Be $false
    }

    It 'estrae nomi lunghi e Unicode da un archivio creato da tar.exe' {
        $sorgente = Join-Path $script:cartellaTest 'sorgente'
        $archivio = Join-Path $script:cartellaTest 'lungo.tgz'
        $destinazione = Join-Path $script:cartellaTest 'output'
        $nome = ('a' * 105) + '-caff' + [char]0x00e8 + '.txt'
        New-Item -ItemType Directory -Path $sorgente | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $sorgente $nome), 'contenuto')
        & tar.exe -czf $archivio -C $sorgente $nome
        if ($LASTEXITCODE -ne 0) {
            throw 'Creazione dell archivio di prova non riuscita.'
        }

        & $script:estrattore $archivio -d $destinazione

        [System.IO.File]::ReadAllText((Join-Path $destinazione $nome)) | Should Be 'contenuto'
    }

    It 'rifiuta percorsi fuori dalla destinazione' {
        $archivio = Join-Path $script:cartellaTest 'percorsi.tgz'
        $destinazione = Join-Path $script:cartellaTest 'output'
        New-TestArchive $archivio @(@{ Name = '../fuori.txt'; Content = 'vietato' })

        { & $script:estrattore $archivio -d $destinazione } | Should Throw
        (Test-Path -LiteralPath (Join-Path $script:cartellaTest 'fuori.txt')) | Should Be $false
    }

    It 'rifiuta un collegamento rigido con il nome della cartella radice' {
        $archivio = Join-Path $script:cartellaTest 'radice.tgz'
        $destinazione = Join-Path $script:cartellaTest 'output'
        New-TestArchive $archivio @(
            @{ Name = 'originale.txt'; Content = 'contenuto' },
            @{ Name = '.'; Type = '1'; LinkName = 'originale.txt' }
        )

        { & $script:estrattore $archivio -d $destinazione } | Should Throw
        (Test-Path -LiteralPath (Join-Path $script:cartellaTest 'duplicati-1')) | Should Be $false
    }

    It 'estrae collegamenti simbolici interni e duplicati anche prima dei relativi file' -Skip:(-not $script:linkDisponibili) {
        $archivio = Join-Path $script:cartellaTest 'simbolico.tgz'
        $destinazione = Join-Path $script:cartellaTest 'output'
        $album = Join-Path $destinazione 'album'
        $link = Join-Path $album 'link.txt'
        $linkDuplicato = Join-Path $album 'duplicati-1\link.txt'
        $linkCartella = Join-Path $album 'link-cartella'
        $linkPadre = Join-Path $album 'nested\link-padre.txt'
        New-Item -ItemType Directory -Path $album -Force | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $album 'foto.txt'), 'esistente')
        New-TestArchive $archivio @(
            @{ Name = 'album/link.txt'; Type = '2'; LinkName = 'foto.txt' },
            @{ Name = 'album/link.txt'; Type = '2'; LinkName = 'foto.txt' },
            @{ Name = 'album/link-cartella'; Type = '2'; LinkName = 'nested' },
            @{ Name = 'album/nested/link-padre.txt'; Type = '2'; LinkName = '../foto.txt' },
            @{ Name = 'album/nested/foto.txt'; Content = 'nella cartella' },
            @{ Name = 'album/foto.txt'; Content = 'nuova' }
        )

        try {
            & $script:estrattore $archivio -d $destinazione

            [System.IO.File]::ReadAllText($link) | Should Be 'nuova'
            [System.IO.File]::ReadAllText($linkDuplicato) | Should Be 'nuova'
            [System.IO.File]::ReadAllText($linkPadre) | Should Be 'nuova'
            [System.IO.File]::ReadAllText((Join-Path $linkCartella 'foto.txt')) | Should Be 'nella cartella'
            [System.IO.File]::ReadAllText((Join-Path $album 'foto.txt')) | Should Be 'esistente'
            ((Get-Item -LiteralPath $link -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) |
                Should Not Be 0
        } finally {
            foreach ($fileLink in @($link, $linkDuplicato, $linkPadre)) {
                if (Test-Path -LiteralPath $fileLink) {
                    [System.IO.File]::Delete($fileLink)
                }
            }
            if (Test-Path -LiteralPath $linkCartella) {
                [System.IO.Directory]::Delete($linkCartella)
            }
        }
    }

    It 'rifiuta collegamenti simbolici verso percorsi esterni' {
        $archivio = Join-Path $script:cartellaTest 'esterno.tgz'
        $destinazione = Join-Path $script:cartellaTest 'output'
        New-TestArchive $archivio @(@{ Name = 'link.txt'; Type = '2'; LinkName = '../fuori.txt' })

        { & $script:estrattore $archivio -d $destinazione } | Should Throw
        (Test-Path -LiteralPath (Join-Path $destinazione 'link.txt')) | Should Be $false
    }

    It 'rifiuta i cicli tra collegamenti simbolici' {
        $archivio = Join-Path $script:cartellaTest 'ciclo.tgz'
        $destinazione = Join-Path $script:cartellaTest 'output'
        New-TestArchive $archivio @(
            @{ Name = 'a.txt'; Type = '2'; LinkName = 'b.txt' },
            @{ Name = 'b.txt'; Type = '2'; LinkName = 'a.txt' }
        )

        { & $script:estrattore $archivio -d $destinazione } | Should Throw
        (Test-Path -LiteralPath (Join-Path $destinazione 'a.txt')) | Should Be $false
    }
}
