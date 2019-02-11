﻿/// Helpers for console interaction: setting colours, parsing input into tokens etc.
module Terminal

open System
open Builtins
open System.IO

/// The starting console colour, before it is overriden by prompts, outputs and help for example.
let originalColour = ConsoleColor.Gray

/// Resets the interface to use the default font colour.
let defaultColour () = 
    Console.ForegroundColor <- originalColour

/// Sets the console foreground colour (font colour) to the colour specified by the given string,
/// e.g. colour "Red" will set the foreground to ConsoleColor.Red.
/// String parsing is only used because its more concise than using the built in enum accessor.
let colour s = 
    let consoleColour = Enum.Parse (typeof<ConsoleColor>, s, true) :?> ConsoleColor
    Console.ForegroundColor <- consoleColour

/// Controls cursor visibility. 
/// The cursor should only be visible when accepting input from the user, and not when drawing the prompt, for example.
let cursor b = Console.CursorVisible <- b

/// Splits up a string into tokens, accounting for escaped spaces and quote wrapping,
/// e.g. '"hello" world "this is a" test\ test' would be processed as ["hello";"world";"this is a";"test test"].
let parts s =
    // The internal recursive function processes the input one char at a time via a list computation expression. 
    // This affords a good deal of control over the output, and is functional/immutable.
    // A slightly simpler way of doing this would be to use a loop with mutables; a commented out version of such an approach is below.
    let rec parts soFar quoted last remainder = 
        [
            if remainder = "" then yield soFar
            else
                let c, next = remainder.[0], remainder.[1..]
                match c with
                | '\"' when soFar = "" -> 
                    yield! parts soFar true last next
                | '\"' when last <> '\\' && quoted ->
                    yield soFar
                    yield! parts "" false last next
                | ' ' when last <> '\\' && not quoted ->
                    if soFar <> "" then yield soFar
                    yield! parts "" quoted last next
                | _ -> 
                    yield! parts (soFar + string c) quoted c next
        ]
    parts "" false ' ' s

/// Reads a line of input from the user, enhanced for automatic tabbing and the like.
/// As tab completion requires we intercept the readkey, we therefore need to implement
/// the rest of the console readline functionality, like arrows and backspace.
let readLine () = 
    let start = Console.CursorLeft

    let common candidates =
        ""

    let attemptTabCompletion soFar pos = 
        let last = parts soFar |> List.last
        let directory = Path.GetDirectoryName (Path.Combine(currentDir (), last))

        let files = Directory.GetFiles directory |> Array.map Path.GetFileName |> Array.toList
        let folders = Directory.GetDirectories directory |> Array.map Path.GetFileName |> Array.toList
        let allOptions = files @ folders @ List.map fst builtins

        let candidates = allOptions |> List.filter (fun n -> n.ToLower().StartsWith(last.ToLower()))

        if candidates.Length = 0 then 
            soFar, pos // no change
        elif candidates.Length = 1 then
            let matched = if candidates.Length = 1 then candidates.[0] else common candidates
            let newPart = matched.Substring last.Length
            (soFar + newPart), (pos + newPart.Length)
        else
            soFar, pos

    let rec reader (soFar: string) pos =
        // By printing out the current content of the line after every char
        // implementing backspace and delete becomes easier.
        cursor false
        Console.CursorLeft <- start
        printf "%s%s" soFar (new String(' ', Console.WindowWidth - start - soFar.Length - 1))
        Console.CursorLeft <- start + pos
        cursor true

        let next = Console.ReadKey(true)

        if next.Key = ConsoleKey.Enter then
            printfn "" // write a final newline
            soFar
        elif next.Key = ConsoleKey.Backspace && Console.CursorLeft <> start then
            reader (soFar.[0..pos-2] + soFar.[pos..]) (max 0 (pos - 1))
        elif next.Key = ConsoleKey.Delete then 
            reader (soFar.[0..pos-1] + soFar.[pos+1..]) pos
        elif next.Key = ConsoleKey.LeftArrow then
            reader soFar (max 0 (pos - 1))
        elif next.Key = ConsoleKey.RightArrow then
            reader soFar (min soFar.Length (pos + 1))
        elif next.Key = ConsoleKey.Home then
            reader soFar 0
        elif next.Key = ConsoleKey.End then
            reader soFar soFar.Length
        elif next.Key = ConsoleKey.Tab && soFar <> "" then 
            let (soFar, pos) = attemptTabCompletion soFar pos
            reader soFar pos
        else
            let c = next.KeyChar
            if not (Char.IsControl c) then
                reader (soFar.[0..pos-1] + string c + soFar.[pos..]) (pos + 1)
            else
                reader soFar pos

    reader "" 0
            

// Mutable version of parts above, done with a loop instead of recursion.
(*
let parts s = 
    [
        let mutable last = ' '
        let mutable quoted = false
        let mutable soFar = ""

        for c in s do
            match c with
            | '\"' when soFar = "" -> quoted <- true
            | '\"' when last <> '\\' && quoted ->
                yield soFar
                quoted <- false
                soFar <- ""
            | ' ' when last <> '\\' && not quoted ->
                if soFar <> "" then yield soFar
                soFar <- ""
            | _ -> 
                soFar <- soFar + string c
                last <- c
            
        if soFar <> "" then yield soFar
    ]*)