import bpy
import math
import mathutils
import json
import copy
import numpy as np
import os
from mathutils import Matrix, Vector
from decimal import Decimal, ROUND_HALF_UP


PROJECT_FILE_PATH = bpy.path.abspath("//../")

JSON_FILE_PATH = bpy.path.abspath("//../") + "/JSON Files/"

EPS = 1e-4

def quantize_vertex(v):
    """Quantizes the components of the vertex, snapping values to an integer grid for reliable and accurate rounding"""
    return (round(v[0] / EPS), round(v[1] / EPS), round(v[2] / EPS))
    
def find_face_vertices(vertices):
    """"""
    direction_dict = directional_dict_copy()
    quantized_boundary = round(1.0/EPS)
    for vertex in vertices:
        quantized_vertex = quantize_vertex(vertex)
        x, y, z = quantized_vertex
        if x == quantized_boundary:
            direction_dict["+x"].append(quantized_vertex)
        if x == -quantized_boundary:
            direction_dict["-x"].append(quantized_vertex)
        if y == quantized_boundary:
            direction_dict["+y"].append(quantized_vertex)
        if y == -quantized_boundary:
            direction_dict["-y"].append(quantized_vertex)
        if z == quantized_boundary:
            direction_dict["+z"].append(quantized_vertex)
        if z == -quantized_boundary:
            direction_dict["-z"].append(quantized_vertex)
    
    return direction_dict

def detect_tile_adjacencies(tiles_face_vertices):
    quantized_addition = round(2/EPS)
    directional_counterparts = {
        "+x": "-x",
        "-x": "+x",
        "+y": "-y",
        "-y": "+y",
        "+z":  "-z",
        "-z": "+z",
    }
    
    counterpart_vectors = {
        "+x":  (quantized_addition, 0, 0),
        "-x": (-quantized_addition, 0, 0),
        "+y":  (0, quantized_addition, 0),
        "-y": (0, -quantized_addition, 0),
        "+z":  (0, 0, quantized_addition),
        "-z": (0, 0, -quantized_addition),
    }
    
    for base_tile, base_data in tiles_face_vertices.items():
        for face, counterpart_face in directional_counterparts.items():
            for opp_tile, opp_data in tiles_face_vertices.items():
                result = [tuple(a + b for a, b in zip(v, counterpart_vectors[face])) for v in opp_data["face_vertices"][counterpart_face]]
                if set(tiles_face_vertices[base_tile]["face_vertices"][face]) == set(result):
                    tiles_face_vertices[base_tile]["adjacency_dict"][face].append(opp_tile)
                                 
    return remove_face_vertices(tiles_face_vertices)

def remove_face_vertices(tiles_face_vertices):
    """Removes face_vertices data from all tiles to reduce memory usage."""
    for tile_data in tiles_face_vertices.values():
        if "face_vertices" in tile_data:
            del tile_data["face_vertices"]
    return tiles_face_vertices

def dump_to_json(dict, specifier_path, parent_path = PROJECT_FILE_PATH):
    absolute_file_path = parent_path + specifier_path
    with open(absolute_file_path, "w") as f:
        json.dump(dict, f, indent=4)

def JSON_to_list(specifier_path, parent_path = PROJECT_FILE_PATH):
    absolute_file_path = parent_path + specifier_path
    if not os.path.isfile(absolute_file_path):
        raise FileNotFoundError(f"JSON file not found: {file_path}")
    
    with open(absolute_file_path, 'r', encoding='utf-8') as handle:
        data_list = json.load(handle)
        if not isinstance(data_list, list):
            raise ValueError(f"Expected JSON list but got {type(data_list).__name__}")
    return data_list

def detect_face_vertices(limited_rotation = []):    
    tile_number_dict = {}
    tile_names_dict = {}
    tile_face_vertices = {}
    index = 0
    for obj in bpy.data.collections["Finalized"].objects:
        if obj.type == 'MESH':
            for rotation in range (1 if obj.name in limited_rotation else 4):
                angle = math.radians(90*rotation)
                rot_z = Matrix.Rotation(angle, 4, 'Z')
                tile_name = obj.name + "_" + str(rotation)
                tile_face_vertices[index] = {
                    "face_vertices": find_face_vertices([rot_z @ v.co for v in obj.data.vertices]),
                    "rotation": rotation,
                    "mesh": obj.name,
                    "adjacency_dict": directional_dict_copy()
                }
                tile_names_dict[index] = tile_name
                tile_number_dict[tile_name] = index
                index += 1

    dump_to_json(tile_names_dict,"tileNumToNam.json", JSON_FILE_PATH)
    dump_to_json(tile_number_dict,"tileNamToNum.json", JSON_FILE_PATH)
        
    return tile_face_vertices       

def blender_to_unity(adjacencies):
    blender_unity_conversion = {
        "+z": "+y",
        "-z": "-y",
        "+y": "+z",
        "-y": "-z"
    }

    corrected_adjacencies = copy.deepcopy(adjacencies)
    
    for tile_name, tile_info in adjacencies.items():
        for original, swap in blender_unity_conversion.items():
            corrected_adjacencies[tile_name]["adjacency_dict"][original] = adjacencies[tile_name]["adjacency_dict"][swap]
    
    return corrected_adjacencies

def export_adjacencies():
    limited_rotation = set(JSON_to_list("rotationExclusions.json", JSON_FILE_PATH))
    tile_face_vertices = detect_face_vertices(limited_rotation)
    tile_adjacencies = detect_tile_adjacencies(tile_face_vertices)
    corrected_tile_adjacencies = blender_to_unity(tile_adjacencies)
    
    dump_to_json(tile_adjacencies,"tileAdjacencies.json", JSON_FILE_PATH)
    dump_to_json(corrected_tile_adjacencies, "correctedTileAdjacencies.json", JSON_FILE_PATH)

def directional_dict_copy():
    return {
        "+x": [],
        "-x": [],
        "+y": [],
        "-y": [],
        "+z": [],
        "-z": [],
    }
    
if __name__ == "__main__":
    export_adjacencies()
