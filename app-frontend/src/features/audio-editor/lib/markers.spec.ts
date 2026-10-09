import { describe, expect, it } from 'vitest'
import {
  exportNames,
  namePart,
  nextMarkerName,
  readMarkers,
  rewriteXmpMarkers,
  spliceMarkers,
  withMarkers,
  type MarkerData,
} from './markers'
import { defaultLayout, encodeWav, parseWav, type WavLayout } from './wav'

const RATE = 44100

function marker(start: number, length: number, name = 'm'): MarkerData {
  return { name, start, length, comment: '', guid: null }
}

/** The shape Audition 13 writes: three tracks, cue markers in the first. */
const AUDITION_XMP = `<?xpacket begin="﻿" id="W5M0MpCehiHzreSzNTczkc9d"?>
<x:xmpmeta xmlns:x="adobe:ns:meta/">
   <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
      <rdf:Description rdf:about=""
            xmlns:dc="http://purl.org/dc/elements/1.1/"
            xmlns:xmpDM="http://ns.adobe.com/xmp/1.0/DynamicMedia/">
         <dc:format>audio/x-wav</dc:format>
         <xmpDM:Tracks>
            <rdf:Bag>
               <rdf:li rdf:parseType="Resource">
                  <xmpDM:trackName>CuePoint Markers</xmpDM:trackName>
                  <xmpDM:trackType>Cue</xmpDM:trackType>
                  <xmpDM:frameRate>f44100</xmpDM:frameRate>
                  <xmpDM:markers>
                     <rdf:Seq>
                        <rdf:li rdf:parseType="Resource">
                           <xmpDM:startTime>28904</xmpDM:startTime>
                           <xmpDM:duration>595673</xmpDM:duration>
                           <xmpDM:comment>Anathema: so &amp; so</xmpDM:comment>
                           <xmpDM:name>001 Ahahaha</xmpDM:name>
                           <xmpDM:cuePointParams>
                              <rdf:Seq>
                                 <rdf:li rdf:parseType="Resource">
                                    <xmpDM:key>marker_guid</xmpDM:key>
                                    <xmpDM:value>xmp:id:674ac6f0</xmpDM:value>
                                 </rdf:li>
                              </rdf:Seq>
                           </xmpDM:cuePointParams>
                           <xmpDM:guid>xmp:id:674ac6f0</xmpDM:guid>
                        </rdf:li>
                        <rdf:li rdf:parseType="Resource">
                           <xmpDM:startTime>666864</xmpDM:startTime>
                           <xmpDM:name> 01</xmpDM:name>
                           <xmpDM:guid>xmp:id:d0d933dd</xmpDM:guid>
                        </rdf:li>
                     </rdf:Seq>
                  </xmpDM:markers>
               </rdf:li>
               <rdf:li rdf:parseType="Resource">
                  <xmpDM:trackName>CD Track Markers</xmpDM:trackName>
                  <xmpDM:trackType>Track</xmpDM:trackType>
                  <xmpDM:frameRate>f44100</xmpDM:frameRate>
               </rdf:li>
            </rdf:Bag>
         </xmpDM:Tracks>
      </rdf:Description>
   </rdf:RDF>
</x:xmpmeta>
<?xpacket end="w"?>`

function layoutWith(chunks: WavLayout['chunks']): WavLayout {
  return { ...defaultLayout(1), chunks: [{ id: 'fmt ' }, { id: 'data' }, ...chunks] }
}

function roundTrip(layout: WavLayout) {
  return parseWav(encodeWav([new Float32Array(10)], RATE, layout)).layout
}

describe('spliceMarkers', () => {
  const markers = [marker(0, 100), marker(200, 100), marker(500, 0), marker(1000, 100)]

  it('shifts markers after a deletion and drops ones inside it', () => {
    expect(spliceMarkers(markers, { start: 150, removed: 400, inserted: 0 })).toEqual([
      marker(0, 100),
      marker(600, 100),
    ])
  })

  it('shortens a range the deletion cuts into', () => {
    expect(spliceMarkers([marker(100, 100)], { start: 150, removed: 100, inserted: 0 })).toEqual([
      marker(100, 50),
    ])
    expect(spliceMarkers([marker(100, 100)], { start: 50, removed: 100, inserted: 0 })).toEqual([
      marker(50, 50),
    ])
  })

  it('pushes markers at or after an insertion and stretches a range spanning it', () => {
    expect(spliceMarkers(markers, { start: 200, removed: 0, inserted: 50 })).toEqual([
      marker(0, 100),
      marker(250, 100),
      marker(550, 0),
      marker(1050, 100),
    ])
    expect(spliceMarkers([marker(100, 100)], { start: 150, removed: 0, inserted: 50 })).toEqual([
      marker(100, 150),
    ])
  })

  it('leaves a range ending exactly where audio is inserted alone', () => {
    expect(spliceMarkers([marker(0, 100)], { start: 100, removed: 0, inserted: 50 })).toEqual([
      marker(0, 100),
    ])
  })
})

describe('readMarkers', () => {
  it('prefers Audition’s XMP, with its names, descriptions and ids', () => {
    const layout = layoutWith([{ id: '_PMX', body: new TextEncoder().encode(AUDITION_XMP) }])

    expect(readMarkers(layout, RATE)).toEqual([
      {
        name: '001 Ahahaha',
        start: 28904,
        length: 595673,
        comment: 'Anathema: so & so',
        guid: 'xmp:id:674ac6f0',
      },
      { name: ' 01', start: 666864, length: 0, comment: '', guid: 'xmp:id:d0d933dd' },
    ])
  })

  it('converts XMP times written at another rate', () => {
    const xml = AUDITION_XMP.replace('f44100', 'f48000')
    const layout = layoutWith([{ id: '_PMX', body: new TextEncoder().encode(xml) }])

    expect(readMarkers(layout, RATE)[0]!.start).toBe(Math.round((28904 * 44100) / 48000))
  })

  it('reads cue and adtl chunks when there is no XMP', () => {
    const written = withMarkers(
      defaultLayout(1),
      [{ ...marker(10, 5, 'Line ü'), comment: 'note' }, marker(3, 0, 'Cue')],
      RATE,
    )

    expect(readMarkers(roundTrip(written), RATE)).toEqual([
      marker(3, 0, 'Cue'),
      { ...marker(10, 5, 'Line ü'), comment: 'note' },
    ])
  })
})

describe('withMarkers', () => {
  it('rewrites cue, adtl and XMP together, keeping the rest of the packet', () => {
    const layout = layoutWith([
      { id: 'cue ', body: new Uint8Array(4) },
      { id: 'LIST', body: new TextEncoder().encode('adtl') },
      { id: '_PMX', body: new TextEncoder().encode(AUDITION_XMP) },
    ])
    const markers = [
      { ...marker(100, 50, 'quest-npc-1'), guid: 'xmp:id:674ac6f0' },
      { ...marker(400, 60, 'A <b> & "c"'), comment: 'said' },
    ]

    const saved = roundTrip(withMarkers(layout, markers, RATE))
    const xml = new TextDecoder().decode(saved.chunks.find((chunk) => chunk.id === '_PMX')!.body)

    expect(saved.chunks.map((chunk) => chunk.id)).toEqual(['fmt ', 'data', 'cue ', 'LIST', '_PMX'])
    expect(readMarkers(saved, RATE)).toEqual([
      markers[0],
      { ...markers[1], guid: expect.stringMatching(/^xmp:id:/) },
    ])
    // The chunks agree with the XMP.
    const withoutXmp = { ...saved, chunks: saved.chunks.filter((chunk) => chunk.id !== '_PMX') }
    expect(readMarkers(withoutXmp, RATE)).toEqual(
      markers.map((entry) => ({ ...entry, guid: null })),
    )
    expect(xml).toContain('<xmpDM:trackName>CD Track Markers</xmpDM:trackName>')
    expect(xml).toContain('<dc:format>audio/x-wav</dc:format>')
    expect(xml.startsWith('<?xpacket begin="﻿"')).toBe(true)
    expect(xml).not.toContain('Ahahaha')
  })

  it('removes every marker chunk when the markers are all gone', () => {
    const layout = withMarkers(
      layoutWith([{ id: '_PMX', body: new TextEncoder().encode(AUDITION_XMP) }]),
      [marker(1, 1)],
      RATE,
    )

    const cleared = withMarkers(roundTrip(layout), [], RATE)

    expect(cleared.chunks.map((chunk) => chunk.id)).toEqual(['fmt ', 'data', '_PMX'])
    expect(readMarkers(cleared, RATE)).toEqual([])
  })

  it('adds a cue track to a packet that has none', () => {
    const xml = AUDITION_XMP.replace(/<xmpDM:Tracks>[\s\S]*<\/xmpDM:Tracks>/, '').replace(
      '\n            xmlns:xmpDM="http://ns.adobe.com/xmp/1.0/DynamicMedia/"',
      '',
    )

    const rewritten = rewriteXmpMarkers(xml, [marker(5, 6, 'x')], RATE)!
    const layout = layoutWith([{ id: '_PMX', body: new TextEncoder().encode(rewritten) }])

    expect(rewritten).toContain('xmlns:xmpDM=')
    expect(readMarkers(layout, RATE)).toEqual([
      { ...marker(5, 6, 'x'), guid: expect.stringMatching(/^xmp:id:/) },
    ])
  })
})

describe('naming', () => {
  it('numbers markers after the highest in use', () => {
    expect(nextMarkerName([])).toBe('Marker 01')
    expect(nextMarkerName([marker(0, 0, 'Marker 09'), marker(0, 0, 'Other')])).toBe('Marker 10')
  })

  it('builds VoW file names from free text', () => {
    expect(namePart('The Corrupted Village!')).toBe('thecorruptedvillage')
    expect(namePart('Élise')).toBe('elise')
    expect(exportNames(3, 'Anathema', 'Zhight', 9)).toEqual([
      'anathema-zhight-9.wav',
      'anathema-zhight-10.wav',
      'anathema-zhight-11.wav',
    ])
  })
})
