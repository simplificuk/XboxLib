using System;
using System.Collections.Generic;
using System.IO;

namespace XboxLib.Compression;

public sealed class LzxDecompression {
	private const uint LzxMinMatch = 2;
	private const uint LzxMaxMatch = 257;
	private const int LzxNumChars = 256;
	private const uint LzxBlockTypeInvalid = 0;
	private const uint LzxBlockTypeVerbatim = 1;
	private const uint LzxBlockTypeAligned = 2;
	private const uint LzxBlockTypeUncompressed = 3;
	private const uint LzxPreTreeNumElements = 20;
	private const uint LzxAlignedNumElements = 8;
	private const int LzxNumPrimaryLengths = 7;
	private const int LzxNumSecondaryLengths = 249;
	private const uint LzxPreTreeMaxSymbols = LzxPreTreeNumElements;
	private const int LzxPreTreeTableBits = 6;
	private const uint LzxMainTreeMaxSymbols = LzxNumChars + 290 * 8;
	private const int LzxMainTreeTableBits = 12;
	private const uint LzxLengthMaxSymbols = LzxNumSecondaryLengths + 1;
	private const int LzxLengthTableBits = 12;
	private const uint LzxAlignedMaxSymbols = LzxAlignedNumElements;
	private const int LzxAlignedTableBits = 7;
	private const uint LzxLenTableSafety = 64;
	private const uint LzxFrameSize = 32768;
	private const int HuffMaxBits = 16;
	private const int BitBufWidth = 32;

	private static readonly uint[] PositionSlots = { 30, 32, 34, 36, 38, 42, 50, 66, 98, 162, 290 };
	private static readonly uint[] ExtraBits = { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10,
		11, 11, 12, 12, 13, 13, 14, 14, 15, 15, 16, 16 };
	private static readonly uint[] PositionBase = { 0, 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64, 96, 128, 192, 256, 384,
		512, 768, 1024, 1536, 2048, 3072, 4096, 6144, 8192, 12288, 16384, 24576, 32768, 49152, 65536, 98304, 131072,
		196608, 262144, 393216, 524288, 655360, 786432, 917504, 1048576, 1179648, 1310720, 1441792, 1572864,
		1703936, 1835008, 1966080, 2097152, 2228224, 2359296, 2490368, 2621440, 2752512, 2883584, 3014656, 3145728,
		3276800, 3407872, 3538944, 3670016, 3801088, 3932160, 4063232, 4194304, 4325376, 4456448, 4587520, 4718592,
		4849664, 4980736, 5111808, 5242880, 5373952, 5505024, 5636096, 5767168, 5898240, 6029312, 6160384, 6291456,
		6422528, 6553600, 6684672, 6815744, 6946816, 7077888, 7208960, 7340032, 7471104, 7602176, 7733248, 7864320,
		7995392, 8126464, 8257536, 8388608, 8519680, 8650752, 8781824, 8912896, 9043968, 9175040, 9306112, 9437184,
		9568256, 9699328, 9830400, 9961472, 10092544, 10223616, 10354688, 10485760, 10616832, 10747904, 10878976,
		11010048, 11141120, 11272192, 11403264, 11534336, 11665408, 11796480, 11927552, 12058624, 12189696,
		12320768, 12451840, 12582912, 12713984, 12845056, 12976128, 13107200, 13238272, 13369344, 13500416,
		13631488, 13762560, 13893632, 14024704, 14155776, 14286848, 14417920, 14548992, 14680064, 14811136,
		14942208, 15073280, 15204352, 15335424, 15466496, 15597568, 15728640, 15859712, 15990784, 16121856,
		16252928, 16384000, 16515072, 16646144, 16777216, 16908288, 17039360, 17170432, 17301504, 17432576,
		17563648, 17694720, 17825792, 17956864, 18087936, 18219008, 18350080, 18481152, 18612224, 18743296,
		18874368, 19005440, 19136512, 19267584, 19398656, 19529728, 19660800, 19791872, 19922944, 20054016,
		20185088, 20316160, 20447232, 20578304, 20709376, 20840448, 20971520, 21102592, 21233664, 21364736,
		21495808, 21626880, 21757952, 21889024, 22020096, 22151168, 22282240, 22413312, 22544384, 22675456,
		22806528, 22937600, 23068672, 23199744, 23330816, 23461888, 23592960, 23724032, 23855104, 23986176,
		24117248, 24248320, 24379392, 24510464, 24641536, 24772608, 24903680, 25034752, 25165824, 25296896,
		25427968, 25559040, 25690112, 25821184, 25952256, 26083328, 26214400, 26345472, 26476544, 26607616,
		26738688, 26869760, 27000832, 27131904, 27262976, 27394048, 27525120, 27656192, 27787264, 27918336,
		28049408, 28180480, 28311552, 28442624, 28573696, 28704768, 28835840, 28966912, 29097984, 29229056,
		29360128, 29491200, 29622272, 29753344, 29884416, 30015488, 30146560, 30277632, 30408704, 30539776,
		30670848, 30801920, 30932992, 31064064, 31195136, 31326208, 31457280, 31588352, 31719424, 31850496,
		31981568, 32112640, 32243712, 32374784, 32505856, 32636928, 32768000, 32899072, 33030144, 33161216,
		33292288, 33423360 };

	private Stream _input;
	private long _offset;
	private long _length;
	private byte[] _window;
	private uint _windowSize;
	private uint _refDataSize;
	private uint _numOffsets;
	private uint _windowPos;
	private uint _framePos;
	private uint _frame;
	private int _resetInterval;
	private int _r0, _r1, _r2;
	private int _blockLength;
	private int _blockRemaining;
	private uint _blockType;
	private bool _headerRead;
	private bool _inputEnd;
	private bool _isDelta;
	private byte[] _inBuf;
	private HuffTable _preTree;
	private HuffTable _mainTree;
	private HuffTable _lengthTree;
	private HuffTable _alignedTree;
	private uint _intelFilesize;
	private uint _intelCurPos;
	private bool _intelStarted;
	private readonly byte[] _e8Buf = new byte[LzxFrameSize];
	private byte[] _o;
	private uint _oOff, _oEnd;
	private uint _iOff, _iEnd;
	private uint _bitBuffer;
	private int _bitsLeft;

	private void ResetState() {
		int i;
		_r0 = 1;
		_r1 = 1;
		_r2 = 1;
		_headerRead = false;
		_blockRemaining = 0;
		_blockType = LzxBlockTypeInvalid;
		for (i = 0; i < LzxMainTreeMaxSymbols; i++)
			_mainTree.Len[i] = 0;
		for (i = 0; i < LzxLengthMaxSymbols; i++)
			_lengthTree.Len[i] = 0;
	}

	public void Init(int windowBits, int resetInterval, int inputBufferSize, long outputLength, bool isDelta,
		IReadOnlyList<byte> windowData) {
		uint windowSize = (uint) (1 << windowBits);
		if (isDelta) {
			if (windowBits is < 17 or > 25)
				throw new ArgumentOutOfRangeException(nameof(windowBits));
		} else {
			if (windowBits is < 15 or > 21)
				throw new ArgumentOutOfRangeException(nameof(windowBits));
		}

		if (resetInterval < 0)
			throw new ArgumentOutOfRangeException(nameof(resetInterval));
		if (outputLength < 0)
			throw new ArgumentOutOfRangeException(nameof(outputLength));
		inputBufferSize = (inputBufferSize + 1) & -2;
		if (inputBufferSize < 2)
			throw new ArgumentOutOfRangeException(nameof(inputBufferSize));
		_windowSize = windowSize;
		_window = new byte[windowSize];
		_inBuf = new byte[inputBufferSize];
		_offset = 0;
		_length = outputLength;
		_refDataSize = 0;
		if (windowData != null) {
			var delta = windowSize - windowData.Count;
			for (var i = 0; i < windowData.Count; i++)
				_window[i + delta] = windowData[i];
			_refDataSize = (uint) _window.Length;
		}
		_windowPos = 0;
		_framePos = 0;
		_frame = 0;
		_resetInterval = resetInterval;
		_intelFilesize = 0;
		_intelCurPos = 0;
		_intelStarted = false;
		_numOffsets = PositionSlots[windowBits - 15] << 3;
		_isDelta = isDelta;
		_o = _e8Buf;
		_oOff = _oEnd = 0;
		_preTree = new HuffTable(this, "preTree", LzxPreTreeMaxSymbols, LzxPreTreeTableBits);
		_mainTree = new HuffTable(this, "mainTree", LzxMainTreeMaxSymbols, LzxMainTreeTableBits);
		_lengthTree = new HuffTable(this, "length", LzxLengthMaxSymbols, LzxLengthTableBits);
		_alignedTree = new HuffTable(this, "aligned", LzxAlignedMaxSymbols, LzxAlignedTableBits);
		ResetState();
		_iOff = _iEnd = 0;
		_bitBuffer = 0;
		_bitsLeft = 0;
		_inputEnd = false;
	}

	public byte[] DecompressLzx(byte[] data) {
		return DecompressLzx(data, null, data.Length * 100);
	}

	public byte[] DecompressLzx(byte[] data, byte[] windowData, long uncompressedSize) {
		var @in = new MemoryStream(data);
		var output = new MemoryStream(new byte[uncompressedSize]);
		var lzx = new LzxDecompression();
		lzx.Init(15, 0, data.Length, uncompressedSize, false, windowData);
		lzx.Decompress(@in, output, uncompressedSize);

		return output.GetBuffer();
	}

	public void Decompress(Stream @in, Stream output, long outBytes) {
		_input = @in;
		var buf = new byte[12];
		if (outBytes < 0)
			throw new ArgumentOutOfRangeException();
		uint i = _oEnd - _oOff;
		if (i > outBytes)
			i = (uint) outBytes;
		if (i > 0) {
			output.Write(_o, (int) _oOff, (int) i);
			_oOff += i;
			_offset += i;
			outBytes -= i;
		}
		if (outBytes == 0)
			return;
		if (_inputEnd) {
			if (_bitsLeft != 16) {
				throw new InvalidOperationException("previous pass overflowed " + _bitsLeft + " bits");
			}
			if (_bitBuffer != 0) {
				throw new InvalidOperationException("non-empty overflowed buffer");
			}
			RemoveBits(_bitsLeft);
			_inputEnd = false;
		}
		var total = _offset + outBytes;
		var endFrame = (int) (total / LzxFrameSize) + (total % LzxFrameSize > 0 ? 1 : 0);
		while (_frame < endFrame) {
			if (_resetInterval > 0 && ((_frame % _resetInterval) == 0)) {
				if (_blockRemaining > 0) {
					throw new IOException($"{_blockRemaining} bytes remaining at reset interval");
				}
				ResetState();
			}
			if (_isDelta) {
				EnsureBits(16);
				RemoveBits(16);
			}

			uint j;
			if (!_headerRead) {
				j = 0;
				i = ReadBits(1);
				if (i > 0) {
					i = ReadBits(16);
					j = ReadBits(16);
				}
				_intelFilesize = (i << 16) | j;
				_headerRead = true;
			}
			var frameSize = LzxFrameSize;
			if (_length > 0 && (_length - _offset) < frameSize) {
				frameSize = (uint) (_length - _offset);
			}
			var bytesTodo = (int) (_framePos + frameSize - _windowPos);
			while (bytesTodo > 0) {
				if (_blockRemaining == 0) {
					if ((_blockType == LzxBlockTypeUncompressed) && (_blockLength & 1) != 0) {
						ReadIfNeeded();
						_iOff++;
					}
					_blockType = ReadBits(3);
					i = ReadBits(16);
					j = ReadBits(8);
					_blockRemaining = _blockLength = (int) ((i << 8) | j);
					switch (_blockType) {
						case LzxBlockTypeAligned:
							for (i = 0; i < 8; i++) {
								_alignedTree.Len[i] = (ushort) ReadBits(3);
							}
							_alignedTree.BuildTable();
							goto case LzxBlockTypeVerbatim;
						case LzxBlockTypeVerbatim:
							_mainTree.ReadLengths(0, LzxNumChars);
							_mainTree.ReadLengths(LzxNumChars, LzxNumChars + _numOffsets);
							_mainTree.BuildTable();
							if (_mainTree.Len[0xE8] != 0)
								_intelStarted = true;
							_lengthTree.ReadLengths(0, LzxNumSecondaryLengths);
							_lengthTree.BuildTableMaybeEmpty();
							break;

						case LzxBlockTypeUncompressed:
							_intelStarted = true;
							if (_bitsLeft == 0)
								EnsureBits(16);
							_bitsLeft = 0;
							_bitBuffer = 0;
							for (i = 0; i < 12; i++) {
								ReadIfNeeded();
								buf[i] = _inBuf[_iOff++];
							}
							_r0 = (buf[0] & 0xFF) | ((buf[1] & 0xFF) << 8) | ((buf[2] & 0xFF) << 16)
							      | ((buf[3] & 0xFF) << 24);
							_r1 = (buf[4] & 0xFF) | ((buf[5] & 0xFF) << 8) | ((buf[6] & 0xFF) << 16)
							      | ((buf[7] & 0xFF) << 24);
							_r2 = (buf[8] & 0xFF) | ((buf[9] & 0xFF) << 8) | ((buf[10] & 0xFF) << 16)
							      | ((buf[11] & 0xFF) << 24);
							break;

						default:
							throw new InvalidOperationException("bad block type");
					}
				}
				var thisRun = _blockRemaining;
				if (thisRun > bytesTodo)
					thisRun = bytesTodo;
				bytesTodo -= thisRun;
				_blockRemaining -= thisRun;
				uint matchLength;
				uint lengthFooter;
				uint extra;
				uint verbatimBits;
				uint mainElement;
				uint runDest;
				uint runSrc;
				uint matchOffset;
				switch (_blockType) {

					case LzxBlockTypeVerbatim:
						while (thisRun > 0) {
							mainElement = (uint) _mainTree.ReadHuffSym();
							// Log.info(String.format("-- this_run=0x%x main_element=0x%x", this_run,
							// main_element));
							if (mainElement < LzxNumChars) {
								_window[_windowPos++] = (byte) mainElement;
								thisRun--;
							} else {
								mainElement -= LzxNumChars;
								matchLength = mainElement & LzxNumPrimaryLengths;
								if (matchLength == LzxNumPrimaryLengths) {
									if (_lengthTree.Empty) {
										throw new InvalidOperationException("LENGTH symbol needed but tree is empty");
									}
									lengthFooter = (uint) _lengthTree.ReadHuffSym();
									matchLength += lengthFooter;
								}
								matchLength += LzxMinMatch;
								switch (matchOffset = mainElement >> 3) {
									case 0:
										matchOffset = (uint) _r0;
										break;
									case 1:
										matchOffset = (uint) _r1;
										_r1 = _r0;
										_r0 = (int) matchOffset;
										break;
									case 2:
										matchOffset = (uint) _r2;
										_r2 = _r0;
										_r0 = (int) matchOffset;
										break;
									case 3:
										matchOffset = 1;
										_r2 = _r1;
										_r1 = _r0;
										_r0 = (int) matchOffset;
										break;
									default:
										extra = (matchOffset >= 36) ? 17 : ExtraBits[matchOffset];
										verbatimBits = ReadBits((int) extra);
										matchOffset = PositionBase[matchOffset] - 2 + verbatimBits;
										_r2 = _r1;
										_r1 = _r0;
										_r0 = (int) matchOffset;
										break;
								}
								if (matchLength == LzxMaxMatch && _isDelta) {
									uint extraLen;
									EnsureBits(3);
									if (PeekBits(1) == 0) {
										RemoveBits(1);
										extraLen = ReadBits(8);
									} else if (PeekBits(2) == 2) {
										RemoveBits(2);
										extraLen = ReadBits(10);
										extraLen += 0x100;
									} else if (PeekBits(3) == 6) {
										RemoveBits(3);
										extraLen = ReadBits(12);
										extraLen += 0x500;
									} else {
										RemoveBits(3);
										extraLen = ReadBits(15);
									}
									matchLength += extraLen;
								}
								if ((_windowPos + matchLength) > _windowSize) {
									throw new IOException("match ran over window wrap");
								}
								runDest = _windowPos;
								i = matchLength;
								if (matchOffset > _windowPos) {
									if (matchOffset > _offset && (matchOffset - _windowPos) > _refDataSize)
										throw new IOException("match offset beyond LZX stream");
									j = (matchOffset - _windowPos);
									if (j > _windowSize) {
										throw new IOException("match offset beyond window boundaries");
									}
									runSrc = _windowSize - j;
									if (j < i) {
										i -= j;
										while (j-- > 0)
											_window[runDest++] = _window[runSrc++];
										runSrc = 0;
									}
									while (i-- > 0)
										_window[runDest++] = _window[runSrc++];
								} else {
									runSrc = (runDest - matchOffset);
									while (i-- > 0)
										_window[runDest++] = _window[runSrc++];
								}
								thisRun -= (int) matchLength;
								_windowPos += matchLength;
							}
						}
						break;

					case LzxBlockTypeAligned:
						while (thisRun > 0) {
							mainElement = (uint) _mainTree.ReadHuffSym();
							if (mainElement < LzxNumChars) {
								_window[_windowPos++] = (byte) mainElement;
								thisRun--;
							} else {
								mainElement -= LzxNumChars;
								matchLength = mainElement & LzxNumPrimaryLengths;
								if (matchLength == LzxNumPrimaryLengths) {
									if (_lengthTree.Empty) {
										throw new InvalidOperationException("LENGTH symbol needed but tree is empty");
									}
									lengthFooter = (uint) _lengthTree.ReadHuffSym();
									matchLength += lengthFooter;
								}
								matchLength += LzxMinMatch;
								switch (matchOffset = mainElement >> 3) {
									case 0:
										matchOffset = (uint) _r0;
										break;
									case 1:
										matchOffset = (uint) _r1;
										_r1 = _r0;
										_r0 = (int) matchOffset;
										break;
									case 2:
										matchOffset = (uint) _r2;
										_r2 = _r0;
										_r0 = (int) matchOffset;
										break;
									default:
										extra = (matchOffset >= 36) ? 17 : ExtraBits[matchOffset];
										matchOffset = PositionBase[matchOffset] - 2;
										int alignedBits;
										switch (extra)
										{
											case > 3:
												extra -= 3;
												verbatimBits = ReadBits((int) extra);
												matchOffset += (verbatimBits << 3);
												alignedBits = _alignedTree.ReadHuffSym();
												matchOffset += (uint) alignedBits;
												break;
											case 3:
												alignedBits = _alignedTree.ReadHuffSym();
												matchOffset += (uint) alignedBits;
												break;
											case > 0:
												verbatimBits = ReadBits((int) extra);
												matchOffset += verbatimBits;
												break;
											default:
												matchOffset = 1;
												break;
										}
										_r2 = _r1;
										_r1 = _r0;
										_r0 = (int) matchOffset;
										break;
								}
								if (matchLength == LzxMaxMatch && _isDelta) {
									uint extraLen;
									EnsureBits(3);
									if (PeekBits(1) == 0) {
										RemoveBits(1);
										extraLen = ReadBits(8);
									} else if (PeekBits(2) == 2) {
										RemoveBits(2);
										extraLen = ReadBits(10);
										extraLen += 0x100;
									} else if (PeekBits(3) == 6) {
										RemoveBits(3);
										extraLen = ReadBits(12);
										extraLen += 0x500;
									} else {
										RemoveBits(3);
										extraLen = ReadBits(15);
									}
									matchLength += extraLen;
								}
								if ((_windowPos + matchLength) > _windowSize) {
									throw new IOException("match ran over window wrap");
								}
								runDest = _windowPos;
								i = matchLength;
								if (matchOffset > _windowPos) {
									if (matchOffset > _offset && (matchOffset - _windowPos) > _refDataSize) {
										throw new IOException("match offset beyond LZX stream");
									}
									j = (matchOffset - _windowPos);
									if (j > _windowSize) {
										throw new IOException("match offset beyond window boundaries");
									}
									runSrc = _windowSize - j;
									if (j < i) {
										i -= j;
										while (j-- > 0)
											_window[runDest++] = _window[runSrc++];
										runSrc = 0;
									}
									while (i-- > 0)
										_window[runDest++] = _window[runSrc++];
								} else {
									runSrc = runDest - matchOffset;
									while (i-- > 0)
										_window[runDest++] = _window[runSrc++];
								}

								thisRun -= (int) matchLength;
								_windowPos += matchLength;
							}
						}
						break;
					case LzxBlockTypeUncompressed:
						runDest = _windowPos;
						_windowPos += (uint) thisRun;
						while (thisRun > 0) {
							if ((i = _iEnd - _iOff) == 0) {
								ReadIfNeeded();
							} else {
								if (i > thisRun)
									i = (uint) thisRun;
								Array.Copy(_inBuf, _iOff, _window, runDest, i);
								runDest += i;
								_iOff += i;
								thisRun -= (int) i;
							}
						}
						break;
					default:
						throw new InvalidOperationException("bad block type"); /* might as well */
				}

				if (thisRun >= 0) continue;
				if (-thisRun > _blockRemaining) {
					throw new IOException(
						$"overrun went past end of block by {-thisRun} ({_blockRemaining} remaining)");
				}
				_blockRemaining -= -thisRun;
			}
			if ((_windowPos - _framePos) != frameSize) {
				throw new IOException($"decode beyond output frame limits! {_windowPos - _framePos} != {frameSize}");
			}
			if (_bitsLeft > 0)
				EnsureBits(16);
			if ((_bitsLeft & 15) != 0)
				RemoveBits(_bitsLeft & 15);
			if (_oOff != _oEnd) {
				throw new IOException($"{_oEnd - _oOff} avail bytes, new {frameSize} frame");
			}
			if (_intelStarted && _intelFilesize != 0 && (_frame <= 32768) && (frameSize > 10)) {
				var data = _e8Buf;
				var dataStart = 0;
				var dataEnd = frameSize - 10;
				var curPos = _intelCurPos;
				var filesize = _intelFilesize;
				_o = data;
				_oOff = 0;
				_oEnd = frameSize;
				Array.Copy(_window, _framePos, data, 0, frameSize);
				while (dataStart < dataEnd) {
					if ((data[dataStart++] & 0xFF) != 0xE8) {
						curPos++;
						continue;
					}
					var absOff = (data[dataStart] & 0xFF) | ((data[dataStart + 1] & 0xFF) << 8)
					                                      | ((data[dataStart + 2] & 0xFF) << 16) | ((data[dataStart + 3] & 0xFF) << 24);
					if ((absOff >= -curPos) && (absOff < filesize)) {
						var relOff = (absOff >= 0) ? absOff - curPos : absOff + filesize;
						data[dataStart + 0] = (byte) (relOff & 0xff);
						data[dataStart + 1] = (byte) ((((uint) relOff) >> 8) & 0xff);
						data[dataStart + 2] = (byte) ((((uint) relOff) >> 16) & 0xff);
						data[dataStart + 3] = (byte) ((((uint) relOff) >> 24) & 0xff);
					}
					dataStart += 4;
					curPos += 5;
				}
				_intelCurPos += frameSize;
			} else {
				_o = _window;
				_oOff = _framePos;
				_oEnd = _framePos + frameSize;
				if (_intelFilesize != 0)
					_intelCurPos += frameSize;
			}
			i = (outBytes < frameSize) ? (uint) outBytes : frameSize;
			output.Write(_o, (int) _oOff, (int) i);
			_oOff += i;
			_offset += i;
			outBytes -= i;
			_framePos += frameSize;
			_frame++;
			if (_windowPos == _windowSize)
				_windowPos = 0;
			if (_framePos == _windowSize)
				_framePos = 0;

		}
		if (outBytes > 0) {
			throw new IOException("bytes left to output");
		}
	}

	private static bool MakeDecodeTable(uint numSymbols, int numBits, IReadOnlyList<ushort> length, IList<short> table) {
		int sym;
		int leaf, fill;
		int bitNum;
		uint pos = 0;
		uint tableMask = ((uint) 1) << numBits;
		var bitMask = tableMask >> 1;
		for (bitNum = 1; bitNum <= numBits; bitNum++) {
			for (sym = 0; sym < numSymbols; sym++) {
				if (length[sym] != bitNum)
					continue;
				leaf = (int) pos;

				if ((pos += bitMask) > tableMask)
					return false;
				for (fill = (int) bitMask; fill-- > 0;)
					table[leaf++] = (short) sym;
			}
			bitMask >>= 1;
		}
		if (pos == tableMask)
			return true;
		for (sym = (int) pos; sym < tableMask; sym++) {
			table[sym] = -1;
		}

		var tmp = tableMask >> 1;
		var nextSymbol = tmp < numSymbols ? numSymbols : tmp;
		pos <<= 16;
		tableMask <<= 16;
		bitMask = 1 << 15;
		for (bitNum = numBits + 1; bitNum <= HuffMaxBits; bitNum++) {
			for (sym = 0; sym < numSymbols; sym++) {
				if (length[sym] != bitNum)
					continue;
				if (pos >= tableMask)
					return false;
				leaf = (int) (pos >> 16);
				for (fill = 0; fill < (bitNum - numBits); fill++) {
					if (table[leaf] == -1) {
						table[(int) (nextSymbol << 1)] = -1;
						table[(int) (nextSymbol << 1) + 1] = -1;
						table[leaf] = (short) nextSymbol++;
					}
					leaf = table[leaf] << 1;
					if (((pos >> (15 - fill)) & 1) != 0)
						leaf++;
				}
				table[leaf] = (short) sym;
				pos += bitMask;
			}
			bitMask >>= 1;
		}
		return pos == tableMask;
	}

	private void ReadLens(IList<ushort> lens, uint first, uint last) {
		uint x, y;
		for (x = 0; x < 20; x++) {
			y = ReadBits(4);
			_preTree.Len[x] = (ushort) y;
		}
		_preTree.BuildTable();

		for (x = first; x < last;)
		{
			var z = _preTree.ReadHuffSym();
			switch (z)
			{
				case 17:
				{
					y = ReadBits(4);
					y += 4;
					while (y-- > 0)
						lens[(int) x++] = 0;
					break;
				}
				case 18:
				{
					y = ReadBits(5);
					y += 20;
					while (y-- > 0)
						lens[(int) x++] = 0;
					break;
				}
				case 19:
				{
					y = ReadBits(1);
					y += 4;
					z = _preTree.ReadHuffSym();
					z = lens[(int) x] - z;
					if (z < 0)
						z += 17;
					while (y-- > 0)
						lens[(int) x++] = (ushort) z;
					break;
				}
				default:
				{
					z = lens[(int) x] - z;
					if (z < 0)
						z += 17;
					lens[(int) x++] = (ushort) z;
					break;
				}
			}
		}
	}

	private void EnsureBits(int numBits) {
		while (_bitsLeft < numBits) {
			ReadBytes();
		}
	}

	private void ReadBytes() {
		ReadIfNeeded();
		var b0 = (uint) (_inBuf[_iOff++] & 0xff);
		ReadIfNeeded();
		var b1 = (uint) (_inBuf[_iOff++] & 0xff);
		var val = (b1 << 8) | b0;
		InjectBits(val, 16);
	}

	private void ReadIfNeeded() {
		if (_iOff >= _iEnd) {
			ReadInput();
		}
	}

	private void ReadInput() {
		var l = _inBuf.Length;
		var read = _input.Read(_inBuf, 0, l);
		if (read <= 0)
		{
			if (_inputEnd)
				throw new EndOfStreamException();
			read = 2;
			_inBuf[0] = _inBuf[1] = 0;
			_inputEnd = true;
		}
		_iOff = 0;
		_iEnd = (uint) read;
	}

	private uint ReadBits(int numBits) {
		EnsureBits(numBits);
		var val = PeekBits(numBits);
		RemoveBits(numBits);

		return val;
	}

	private uint PeekBits(int numBits) {
		var result = _bitBuffer >> (BitBufWidth - numBits);
		return result;
	}

	private void RemoveBits(int numBits) {
		_bitBuffer <<= numBits;
		_bitsLeft -= numBits;
	}

	private void InjectBits(uint bitData, int numBits) {
		_bitBuffer |= bitData << (BitBufWidth - numBits - _bitsLeft);
		_bitsLeft += numBits;
	}

	private class HuffTable
	{
		private readonly LzxDecompression _parent;
		private readonly string _tbl;
		private readonly int _tableBits;
		private readonly uint _maxSymbols;
		private readonly short[] _table;
		internal readonly ushort[] Len;
		internal bool Empty;

		internal HuffTable(LzxDecompression owner, string tbl, uint maxSymbols, int tableBits)
		{
			_parent = owner;
			_tbl = tbl;
			_maxSymbols = maxSymbols;
			_tableBits = tableBits;
			_table = new short[(1 << tableBits) + (maxSymbols * 2)];
			Len = new ushort[maxSymbols + LzxLenTableSafety];
		}

		internal void BuildTable() {
			if (!MakeDecodeTable(_maxSymbols, _tableBits, Len, _table)) {
				throw new InvalidOperationException($"failed to build {_tbl} table");
			}
			Empty = false;
		}

		internal void BuildTableMaybeEmpty() {
			Empty = false;
			if (MakeDecodeTable(_maxSymbols, _tableBits, Len, _table)) return;
			for (var i = 0; i < _maxSymbols; i++) {
				if (Len[i] > 0) {
					throw new InvalidOperationException($"failed to build {_tbl} table");
				}
			}
			Empty = true;
		}

		internal void ReadLengths(uint first, uint last) {
			_parent.ReadLens(Len, first, last);
		}

		internal int ReadHuffSym() {
			_parent.EnsureBits(HuffMaxBits);
			var sym = _table[_parent.PeekBits(_tableBits)] & 0xFFFF;
			if (sym >= _maxSymbols)
				sym = HuffTraverse(sym);
			_parent.RemoveBits(Len[sym]);
			return sym;
		}

		private int HuffTraverse(int sym) {
			var i = 1 << (BitBufWidth - _tableBits);
			do {
				if ((i >>= 1) == 0) {
					throw new InvalidOperationException("huffTraverse");
				}
				sym = _table[(sym << 1) | (((_parent._bitBuffer & i) != 0) ? 1 : 0)];
			} while (sym >= _maxSymbols);
			return sym;
		}
	}
}