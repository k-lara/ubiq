from pydoc import cli
import numpy as np
import glob
import struct
from scipy.io.wavfile import write, read
import matplotlib.pyplot as plt
import os
import shutil
# from pydub import AudioSegment
import subprocess

def to_wav(files):
    byteorder = "little"
    
    for file in files:
        
        filename = os.path.basename(file).split('.')[0]

        clips = {0 : [], 1 : [], 2 : [], 3 : [], 4 : [], 5 : [], 6 : []}
        f = open(file, "rb")
        print(file)

        bytes = f.read()
        print("lenght", len(bytes))
        i = 0
        # write audio into separate clips
        while (i < len(bytes)):
            # get package size
            len_pckg = bytes[i:i+4]
            l = int.from_bytes(len_pckg, byteorder)
            i = i + 4
            clip_nr = bytes[i:i+2]
            c = int.from_bytes(clip_nr, byteorder)
            i = i + 2
            audio = bytes[i:i+l-2]

            print(len(audio))
            s = 0
            while (s < len(audio)): # !!!!!!!!!!!
                
                # print(s)
                pcm = audio[s:s+2]
                # shortSample = int.from_bytes(pcm, byteorder, signed=True)
                # print(pcm)
                shortSample = np.frombuffer(pcm, np.int16)
                # print(shortSample)
                s = s + 2 
                # floatSample = shortSample[0] / 32767
                # floatSample = np.clip(floatSample * 1.0, -.999, .999)
                clips[c].append(shortSample)

            i = i + l - 2
            print("len pckg and clipnr ", l, c, i, len_pckg, clip_nr)
        f.close()
        
        # x = np.arange(0,len(clips[0]))
        # print(type(x))
        # plt.plot(x, clips[0])
        # plt.show()

        # print(clips[0])
        print(type(clips[0]))
        print("clip lengths: ", len(clips[0]), len(clips[1]), len(clips[2]), len(clips[3]), len(clips[4]), len(clips[5]))

        # write to wav
        for key in clips:
            if (len(clips[key]) > 0):
                data = np.asarray(clips[key])
                print("Write clip {} to .wav file".format(key))
                path = os.path.join(wav_folder, filename + "_{}.wav".format(key))
                write(path, 16000, data.astype(np.float32))

def organize_downloaded(files):

    for file in files:
        
        filename = os.path.basename(file).split('.')[0]
        # print(file)
        dir_name = "_".join(filename.split("_", 2)[:2])
        new_file = "-".join(filename.split("-", 5)[:5])

        # print(mp3_to_wav_folder)
        # print(dir_name)
        new_dir = os.path.join(mp3_to_wav_folder, dir_name)
        if not os.path.exists(new_dir):
            os.makedirs(new_dir)
            print("new dir: " + new_dir)
        
        new = os.path.join(new_dir, new_file) + ".wav"
        subprocess.call(['ffmpeg', '-i', file , '-ar', '16000', 
                 new])
        print("Added: " + new)

def wav_to_raw_data(rootdir):

    for subdir, dirs, files in os.walk(rootdir):
        print("Dir: " + subdir)
        print(len(files))

        if (len(files) >= 1):
            dirname = os.path.basename(subdir).split('.')[0] + ".dat"
            # new_filename = subdir.rsplit("_", 1)[0] + ".dat"

            f = open(os.path.join(transformed, dirname), "wb")
            # stitch individual wav files back together
            for file in files: # !!! These files are without directory prepended
                print(file)
                filename = os.path.basename(file).split('.')[0]
                # print("rsplit " + new_filename)

                clip_nr = filename.rsplit('_', 1)[-1]
                clip_nr = np.array([clip_nr]).astype(np.int16)
                # print(clip_nr.tobytes())
                print("Clipnr: {}, dtype: {}".format(clip_nr, clip_nr.dtype))

                samplerate, data = read(os.path.join(subdir, file))
                print("sample rate: {}".format(samplerate))

                samples_per_package = 32000
                num_packages = (len(data)) // samples_per_package
                print("data length in bytes {}, pckg size {}, num pckgs {}".format(len(data), samples_per_package, num_packages)) # is slightly shorter than original, but should be fine as long as it does not get longer

                package = samples_to_bytes(clip_nr, data, num_packages, samples_per_package)

                f.write(package)
            f.close()


def samples_to_bytes(clip_nr, data, num_packages, samples_per_package):
    # num_packages = num_samples / samples_package
    packages = []

    # num_packages will be rounded down to int so we have some samples at the end that we have to add too
    bytes_clipnr = clip_nr.tobytes()
    for i in range(0,num_packages):
        start = i * samples_per_package
        end = (i + 1) * samples_per_package
        pckg = data[start:end]
        bytes_pckg = pckg.tobytes()
        bytes_size = np.array([len(bytes_pckg) + 2]).tobytes() # this includes the 2 byte clip nr too!
        packages.append(bytes_size)
        packages.append(bytes_clipnr)
        packages.append(pckg.tobytes())
        print("byte size {}, clipnr {}".format(bytes_size, bytes_clipnr))
    
    current_index = num_packages * samples_per_package # data until now
    if (current_index < len(data)): # then we need to add a bit of rest data that didn't fit in the previous packages
        rest = data[current_index:]
        bytes_pckg = pckg.tobytes()
        bytes_size = np.array([len(bytes_pckg) + 2]).tobytes() # this includes the 2 byte clip nr too!
        packages.append(bytes_size)
        packages.append(bytes_clipnr)
        packages.append(bytes_pckg)
        print("byte size {}, clipnr {}".format(bytes_size, bytes_clipnr))
    
    byte_array = np.asarray(packages)
    print(byte_array.shape)

    return byte_array
    



if __name__ == "__main__":

    folder = "C:/Users/klara/PhD/Projects/ubiqFork/AudioProcessing/original"
    wav_folder = "C:/Users/klara/PhD/Projects/ubiqFork/AudioProcessing/wav"

    mp3_to_wav_folder = "C:/Users/klara/PhD/Projects/ubiqFork/AudioProcessing/mp3_to_wav"

    transformed = "C:/Users/klara/PhD/Projects/ubiqFork/AudioProcessing/transformed"

    files = glob.glob(folder + "/audiorec*") # get audio files

    downloaded  = "C:\\Users\\klara\\PhD\\Projects\\ubiqFork\\AudioProcessing\\downloaded"
    


    # newFileNames = []
    # for file in mp3s:
    #     new = "-".join(file.split("-", 5)[:5])
    #     original = "_".join(new.split("_", 2)[:2])
    #     print(file)
    #     print(new)
    #     print(original)
    #     newFileNames.append(new)

    # infos = glob.glob(folder + "/IDs*")
    # separate audio tracks 

    # to_wav(files)

    # mp3s = glob.glob(downloaded + "/audiorec*")

    # organize_downloaded(mp3s)

    wav_to_raw_data(mp3_to_wav_folder)
   


