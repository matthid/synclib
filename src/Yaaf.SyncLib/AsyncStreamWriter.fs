// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib

/// A simple AsyncStreamWriter class for asynchronous stream writing
[<Sealed>]
type AsyncStreamWriter(stream:System.IO.Stream, encoding:System.Text.Encoding, ?newLine) =
    /// The newline character
    let newLine = defaultArg newLine System.Environment.NewLine
    /// indicating whether we have written the encoding preamble
    let mutable preamblewritten = stream.CanSeek && stream.Position > 0L
    /// Writes the given string encoded in the stream
    let write (line:string) = async {
        if not preamblewritten then
            let p = encoding.GetPreamble()
            do! stream.AsyncWrite(p, 0, p.Length)
            preamblewritten <- true
        let bytes = encoding.GetBytes(line)
        do! stream.AsyncWrite(bytes,0,bytes.Length)
        }
    /// Writes the given string into stream with the current encoding
    member x.Write (line:string) = write line
    /// Writes the given string followed by a newline character into the stream
    member x.WriteLine (line:string) = write (line + newLine)
