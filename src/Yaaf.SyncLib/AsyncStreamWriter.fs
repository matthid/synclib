// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib

[<Sealed>]
type AsyncStreamWriter(stream:System.IO.Stream, encoding:System.Text.Encoding, ?newLine) =
    let newLine = defaultArg newLine System.Environment.NewLine
    let mutable preamblewritten = stream.CanSeek && stream.Position > 0L
        
    let write (line:string) = async {
        if not preamblewritten then
            let p = encoding.GetPreamble()
            do! stream.AsyncWrite(p, 0, p.Length)
            preamblewritten <- true
        let bytes = encoding.GetBytes(line)
        do! stream.AsyncWrite(bytes,0,bytes.Length)
        }

    member x.Write (line:string) = write line
    member x.WriteLine (line:string) = write (line + newLine)
