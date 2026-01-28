# rANS Compression

A .NET 10 implementation of the rANS (range Asymmetric Numeral Systems) compression algorithm.

It is a partial C# port of Fabian "ryg" Giesen's, Feb 2014, C++ implementation found at: https://github.com/rygorous/ryg_rans

The actual theory behind rANS can be found in the original paper by Jarek Duda:
"Asymmetric numeral systems" (http://arxiv.org/abs/1311.2540)

This code is evolved a bit from the original C++ code in the sense that, whereas the original is
primary a demonstration of the algorithm, this code can actually be used directly,
encoding and decoding are separated into their own methods. 

Both encoding and decoding require access to the symbol frequency table to work.
The symbol frequency table can of course be readily generated from the input data in encoder, 
but it needs to somehow be transmitted to the decoder as well.

The basic encoding Rans.Encode() method returns the frequency table (uint[]) as an out parameter. 
The Rans.Decode() method takes the frequency table as an input parameter along with the compressed data.

As a convenience, another class, PackagedRans, is provided that packages the frequency table along 
with the encoded data into a single byte array. It does this by compressing the frequency table represented
as an array of bits with r/ANS again, and then finally the frequency table of that compression. This is not
optimal, but it allows for storing one simple byte array that contains everything needed for decoding.
If you have a scenario where the decoder can infer the frequency table from context, you should be using
the basic Rans.Encode() and Rans.Decode() methods instead.

Like the code it is based on, this code is public domain.

## Features
- Fast byte-level compression
- Encode/decode support

## Usage
See Program.cs for an example of how to use the Rans and PackagedRans classes - it is very short.

A version of Edgar Alan Poe's poem "The Raven" is included as a test file (theraven.txt) for verification.
Compression with the "naked" Rans encoding yields a ratio of 56,7266%.
Compression with the "packaged" Rans encoding yields a ratio of 59,4830%.

These ratios are not impressive when compared to general purpose archivers, like ZIP, but you have to
consider that, like Huffman or arithmetic coding, r/ANS's compressed is based on symbol probabilities alone
whereas ZIP and others use dictionaries to identify entropy across individual symbols.

