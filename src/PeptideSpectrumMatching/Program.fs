namespace ProteomIQon

open System.IO
open CLIArgumentParsing
open Argu
open PeptideSpectrumMatching
module console1 =
    open BioFSharp.Mz

    [<EntryPoint>]
    let main argv = 
        printfn "%A" argv

        let parser = ArgumentParser.Create<CLIArguments>(programName =  (System.Reflection.Assembly.GetExecutingAssembly().GetName().Name)) 
        let results = parser.Parse argv
        let i = results.GetResult InstrumentOutput
        let o = results.GetResult OutputDirectory
        let p = results.GetResult ParamFile
        let d = results.GetResult PeptideDataBase
        Directory.CreateDirectory(o) |> ignore
        let logger = Logging.createLogger (sprintf @"%s\run_log.txt" o) "PeptideSpectrumMatching"
        logger.Info (sprintf "InputFilePath -i = %s" i)
        logger.Info (sprintf "InputFilePath -o = %s" o)
        logger.Info (sprintf "InputFilePath -p = %s" p)
        logger.Trace (sprintf "CLIArguments: %A" results)
        let p = 
            Json.ReadAndDeserialize<Dto.PeptideSpectrumMatchingParams> p
            |> Dto.PeptideSpectrumMatchingParams.toDomain
        let dbConnection = 
            if File.Exists d then
                logger.Trace (sprintf "Database found at given location (%s)" d)
                SearchDB.getDBConnection d
            else
                failwith "The given path to the instrument output is neither a valid file path nor a valid directory path."

        if File.Exists i then
            logger.Info (sprintf "single file")
            logger.Trace (sprintf "Scoring spectra for %s" i)
            scoreSpectra p o dbConnection i
        elif Directory.Exists i then 
            logger.Info (sprintf "multiple files")
            let files = 
                Directory.GetFiles(i,("*.mzlite"))
            logger.Trace (sprintf "Scoring multiple files: %A" files)
            let c = 
                match results.TryGetResult Parallelism_Level with 
                | Some c    -> c
                | None      -> 1
            logger.Trace (sprintf "Program is running on %i cores" c)
            files 
            |> FSharpAux.PSeq.map (scoreSpectra p o dbConnection) 
            |> FSharpAux.PSeq.withDegreeOfParallelism c
            |> Array.ofSeq
            |> ignore
        else 
            failwith "The given path to the instrument output is neither a valid file path nor a valid directory path."

        logger.Info "Hit any key to exit."
        System.Console.ReadKey() |> ignore
        0