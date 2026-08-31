with open('/home/admin1/Aqevryn-csharp/src/Aqevryn/Api/WebDashboard.cs', 'r') as f:
    content = f.read()

# Count occurrences
print("switchTab count:", content.count("function switchTab"))
print("loadCompletedResearch:", "loadCompletedResearch" in content)
print("GetCompletedResearch:", "GetCompletedResearch" in content)
print("completed-research:", "completed-research" in content)
print("Concluded Research:", "Concluded Research" in content)
print("GetCompletedResearch method:", "object GetCompletedResearch" in content)