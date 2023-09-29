using System;
using System.IO;
using System.Linq;
using System.Text;
using XboxLib.Xbe;

public static class PatchAttach
{
    /***
     * Patches an attach.xbe to contain information copied from the game xbe - titleid, title, image, etc.
     */
    public static byte[] Patch(Stream attachXbeSource, Stream gameXbeSource)
    {
        // Copy XBE to memory
        using var outputStream = new MemoryStream();
        attachXbeSource.Position = 0;
        attachXbeSource.CopyTo(outputStream);

        
        outputStream.Position = 0;
        var attachXbe = XbeFile.Read(outputStream);
        var attachReader = new BinaryReader(outputStream);
        var outputWriter = new BinaryWriter(outputStream);

        var gameXbe = XbeFile.Read(gameXbeSource);
        var gameReader = new BinaryReader(gameXbeSource);

        // Copy certificate
        gameXbeSource.Position = gameXbe.CertAddress - gameXbe.BaseAddress;
        outputStream.Position = attachXbe.CertAddress - attachXbe.BaseAddress;
        outputWriter.Write(gameReader.ReadBytes(464));

        // Todo: Rocky5 changes the attach.xbe cert version...? why?

        // Copy sections - $$XSIMAGE, $$XTIMAGE, $$XTINFO and update sizes
        var sectionsToCopy =
            gameXbe.Sections.Values.Where(section => section.Name is "$$XSIMAGE" or "$$XTIMAGE" or "$$XTINFO").ToArray();
        var newSections = sectionsToCopy.Where(section => !attachXbe.Sections.ContainsKey(section.Name)).ToArray();
        
        // Todo: update existing sections if they exist
        var sectionUpdates = sectionsToCopy.Where(section => attachXbe.Sections.ContainsKey(section.Name));
        if (sectionUpdates.Any())
        {
            throw new Exception("Not sure how to update existing sections yet...");
        }

        var sectionHeaderSizeAmendment = newSections.Count() * 56;
        var sectionSizeAdjustment = sectionsToCopy.Sum(section =>
        {
            if (attachXbe.Sections.TryGetValue(section.Name, out var existingSection))
            {
                return section.RawSize - existingSection.RawSize;
            }

            return section.RawSize;
        });

        // We'll assume that the attach XBE has enough padding for our new section info so we don't have to relocate
        // the rest of the file...
        var sectionsStart = attachXbe.SectionAddress - attachXbe.BaseAddress;
        var existingSectionsEnd = sectionsStart + attachXbe.Sections.Count * 56;
        var newSectionsEnd = existingSectionsEnd + newSections.Count() * 56;
        var nextSectionNameAddress = (uint)newSectionsEnd;

        // Relocate existing section names if necessary
        for (var i = 0; i < attachXbe.Sections.Count; i++)
        {
            var namePosOff = sectionsStart + i * 56 + 20;
            outputStream.Position = namePosOff;
            var nameAddr = attachReader.ReadUInt32() - attachXbe.BaseAddress;
            if (nameAddr >= newSectionsEnd) continue;
            
            // Relocate
            var namePos = nextSectionNameAddress;
            
            // Write the new name position back to the section header
            outputStream.Position = namePosOff;
            outputWriter.Write(namePos + attachXbe.BaseAddress);
            
            // Write the section name to the new location
            outputStream.Position = namePos;
            outputWriter.Write(Encoding.ASCII.GetBytes(attachXbe.Sections.Values.ElementAt(i).Name));
            outputWriter.Write((byte)0);
            
            nextSectionNameAddress = (uint)outputStream.Position;
        }

        outputStream.Position = existingSectionsEnd;
        foreach (var section in newSections)
        {
            var dataAddr = (uint)outputStream.Length;
            outputWriter.Write((uint)section.Flags);
            outputWriter.Write(attachXbe.BaseAddress + dataAddr);
            outputWriter.Write(section.VirtualSize);
            outputWriter.Write(dataAddr);
            outputWriter.Write(section.RawSize);
            var pos = outputStream.Position;

            // Write section data
            outputStream.Position = outputStream.Length;
            outputStream.Write(gameXbe.SectionData(section.Name));

            // Write section name
            var namePos = nextSectionNameAddress;
            outputStream.Position = namePos;
            outputWriter.Write(Encoding.ASCII.GetBytes(section.Name));
            outputWriter.Write((byte)0);
            nextSectionNameAddress = (uint)outputStream.Position;
            outputStream.Position = pos;

            outputWriter.Write(namePos + attachXbe.BaseAddress);
            outputWriter.Write((uint)0);
            outputWriter.Write((uint)0);
            outputWriter.Write((uint)0);
            outputWriter.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        }

        // Set correct number of sections
        outputStream.Position = 0x11c;
        outputWriter.Write((uint)(attachXbe.Sections.Count + newSections.Count()));

        // Adjust size of headers
        outputStream.Position = 0x108;
        var headerSize = attachReader.ReadUInt32();
        outputStream.Position = 0x108;
        outputWriter.Write(headerSize + sectionHeaderSizeAmendment);
        outputStream.Position = 0x10c;
        var imageSize = attachReader.ReadUInt32();
        outputStream.Position = 0x10c;
        outputWriter.Write(imageSize + sectionSizeAdjustment);

        return outputStream.ToArray();
    }
}